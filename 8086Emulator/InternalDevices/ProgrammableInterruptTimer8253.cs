using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Masch._8086Emulator.InternalDevices
{
  // see https://wiki.osdev.org/Programmable_Interval_Timer
  public class ProgrammableInterruptTimer8253 : IInternalDevice
  {
    public const int Frequency = 1_193_182; // Hz
    private const int Irq0Timer = 0;

    private const int DramRefreshTimer = 1;
    private const int PcSpeakerTimer = 2;
    private const int TimerCount = PcSpeakerTimer + 1;
    private readonly ProgrammableInterruptController8259 pic;
    private readonly Timer[] timers;
    private readonly List<FutureAction> futureActions = new List<FutureAction>();
    private Task beepTask = Task.CompletedTask;

    private class FutureAction
    {
      public int Ticks;
      public Action Action;
    }

    public ProgrammableInterruptTimer8253(ProgrammableInterruptController8259 pic)
    {
      this.pic = pic;
      timers = new Timer[TimerCount];
      timers[Irq0Timer] = new Timer();
      timers[DramRefreshTimer] = new Timer();
      timers[PcSpeakerTimer] = new Timer();
    }

    public void PostAction(Action action, int delayTicks = 100)
    {
      futureActions.Add(new FutureAction { Ticks = delayTicks, Action = action });
    }

    public void PostAction(Action action, TimeSpan delay)
    {
      futureActions.Add(new FutureAction { Ticks = (int)(delay.TotalSeconds * Frequency), Action = action });
    }

    public int SpeakerFrequency
    {
      get
      {
        var timer = timers[PcSpeakerTimer];
        return timer.IsActive && timer.InitialValue > 0 ? Frequency / timer.InitialValue : 0;
      }
    }

    IEnumerable<int> IInternalDevice.PortNumbers => Enumerable.Range(0x40, 4);

    public void Tick()
    {
      for (var i = 0; i < TimerCount; i++)
      {
        var timer = timers[i];
        if (timer.IsActive)
        {
          switch (timer.Mode)
          {
            case Mode.InterruptOnTerminalCount:
              if (--timers[i].Counter == 0 && i == Irq0Timer)
              {
                pic.Invoke(Irq.Timer);
              }
              break;
            case Mode.OneShot:
              if (i == PcSpeakerTimer)
              {
                // TODO implement
              }
              break;
            case Mode.RateGenerator:
            case Mode.RateGenerator2:
              if (--timer.Counter == 1) { timer.Counter = timer.InitialValue; }
              break;
            case Mode.SquareWaveGenerator:
            case Mode.SquareWaveGenerator2:
              timer.Counter -= 2;
              if (timer.Counter <= 0) { timer.Counter = timer.InitialValue; }
              break;
            case Mode.SoftwareTriggeredStrobe:
              // TODO implement
              break;
            case Mode.HardwareTriggeredStrobe:
              if (i == PcSpeakerTimer)
              {
                // TODO implement
              }
              break;
          }
        }
      }

      var j = 0;
      while (j < futureActions.Count)
      {
        if (--futureActions[j].Ticks == 0)
        {
          futureActions[j].Action();
          futureActions.RemoveAt(j);
        }
        else
        {
          j++;
        }
      }
    }

    byte IInternalDevice.GetByte(int port)
    {
      byte result = 0;

      if (port >= 0x40 && port <= 0x42)
      {
        var timer = timers[port & 0b11];

        var value = timer.Type == AccessType.Latch ? timer.Latch ?? 0 : timer.Counter;

        result = timer.WordPart == WordPart.Lo ? (byte)value : (byte)(value >> 8);

        if (timer.Type == AccessType.Latch || timer.Type == AccessType.LoHiValue)
        {
          // ReSharper disable once BitwiseOperatorOnEnumWithoutFlags
          timer.WordPart ^= WordPart.Hi;
        }
      }

      return result;
    }

    void IInternalDevice.SetByte(int port, byte value)
    {
      if (port >= 0x40 && port <= 0x42)
      {
        var timer = timers[port & 0b11];

        if (timer.WordPart == WordPart.Lo)
        {
          timer.InitialValue = (ushort)((timer.InitialValue & 0xFF00) | value);
        }
        else
        {
          timer.InitialValue = (ushort)((value << 8) | (timer.InitialValue & 0x00FF));
        }
        if (timer.Type == AccessType.LoHiValue)
        {
          timer.IsActive = timer.WordPart == WordPart.Hi;
          // ReSharper disable once BitwiseOperatorOnEnumWithoutFlags
          timer.WordPart ^= WordPart.Hi;
        }
        else
        {
          timer.IsActive = true;
        }

        if (timer.IsActive)
        {
          timer.Latch = null;
          timer.Counter = timer.InitialValue;

          if ((port & 0b11) == 2 && beepTask.IsCompleted)
          {
            // The sound should actually be played until it gets deactivated by a call to port 0x61
            beepTask = Task.Run(() => Console.Beep(Frequency / timer.InitialValue, 250));
          }
        }
      }
      else if (port == 0x43) // control word register
      {
        var channel = value >> 6;
        if (channel == 3) { return; } // read-back command of 8254

        if ((value & 0b00111111) == 0)
        {
          timers[channel].Latch = timers[channel].Counter;
          return;
        }

        var timer = timers[channel];
        timer.Type = (AccessType)((value >> 4) & 0b11);
        timer.Mode = (Mode)((value >> 1) & 0b111);
        timer.WordPart = WordPart.Lo;
      }
    }

    private class Timer
    {
      public ushort Counter;
      public ushort InitialValue;
      public bool IsActive;
      public ushort? Latch;
      public Mode Mode;
      public AccessType Type;
      public WordPart WordPart;

      // ReSharper disable once UnusedMember.Local
      public TimeSpan Duration
      {
        get
        {
          var divisor = InitialValue == 0 ? 0x10000 : InitialValue;
          return TimeSpan.FromSeconds((double)divisor / Frequency);
        }
      }
    }

    private enum Mode
    {
      InterruptOnTerminalCount,
      OneShot,
      RateGenerator,
      SquareWaveGenerator,
      SoftwareTriggeredStrobe,
      HardwareTriggeredStrobe,
      RateGenerator2,
      SquareWaveGenerator2
    }

    private enum WordPart
    {
      Lo,
      Hi
    }

    private enum AccessType
    {
      Latch,

      // ReSharper disable once UnusedMember.Local
      LoValue,

      // ReSharper disable once UnusedMember.Local
      HiValue,
      LoHiValue
    }
  }
}