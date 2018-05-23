#define _SPEEDLIMIT
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Masch.Emulator8086.CPU;
using Masch.Emulator8086.InternalDevices;

namespace Masch.Emulator8086
{
  public class Machine
  {
    private bool running;
    private CancellationTokenSource shutdownCts;

    public Machine()
    {
      MemoryController = new MemoryController();
      Cpu = new Cpu8086(this);
      Ports = new Dictionary<int, IInternalDevice>();

      Reboot();
    }

    public Cpu Cpu { get; }
    public MemoryController MemoryController { get; }
    public CrtController6845 Graphics { get; private set; }
    public ProgrammableInterruptController8259 Pic { get; private set; }
    public ProgrammableInterruptTimer8253 Pit { get; private set; }

    public Dictionary<int, IInternalDevice> Ports { get; }

    public void LoadAndSetBootstrapper(int segment, byte[] bytes)
    {
      LoadProgram(segment, bytes);

      var ofs = SpecialOffset.BootStrapping;
      MemoryController.WriteByte(ofs++, 0xEA); // JMP FAR
      MemoryController.WriteWord(ofs += 2, 0); // offset
      MemoryController.WriteWord(ofs, (ushort)segment); // segment
    }

    public void LoadProgram(int segment, byte[] bytes)
    {
      if (segment < 0 || segment > ushort.MaxValue) { throw new ArgumentOutOfRangeException(nameof(segment)); }
      if (bytes == null) { throw new ArgumentNullException(nameof(bytes)); }

      MemoryController.WriteBlock(segment << 4, bytes, bytes.Length);
    }

    public void Reboot()
    {
      shutdownCts?.Cancel();
      shutdownCts = new CancellationTokenSource();

      Cpu.Reset();
      Ports.Clear();
      ConnectInternalDevices();
    }

    public void Run()
    {
      running = true;
      var watch = Stopwatch.StartNew();
      var opcodeCount = 0;

#if SPEEDLIMIT
      var lastElapsed = watch.Elapsed;
      var lastClockCount = Cpu.ClockCount;
      var measurementTime = TimeSpan.FromMilliseconds(10);
#endif

      while (running)
      {
        Cpu.Tick();
        opcodeCount++;

#if SPEEDLIMIT
        var elapsed = watch.Elapsed;
        var realTime = elapsed - lastElapsed;
        if (realTime >= measurementTime)
        {
          var cpuTime = TimeSpan.FromSeconds((Cpu.ClockCount - lastClockCount) / (double)Cpu.Frequency);
          var waitTime = cpuTime - realTime;
          if (waitTime >= TimeSpan.Zero) { Thread.Sleep(waitTime); }

          lastElapsed = elapsed;
          lastClockCount = Cpu.ClockCount;
        }
#endif
      }
      shutdownCts.Cancel();
      watch.Stop();
      Console.WriteLine($"\r\nProcessed {opcodeCount:N0} opcodes in {watch.Elapsed} ({opcodeCount / watch.Elapsed.TotalSeconds:N0} op/s, {Cpu.ClockCount / watch.Elapsed.TotalSeconds / 1024 / 1024:F2} MHz)");
    }

    public void Stop()
    {
      running = false;
    }

    private void ConnectInternalDevices()
    {
      Pic = new ProgrammableInterruptController8259();
      Pit = new ProgrammableInterruptTimer8253(Pic);
      Graphics = new CrtController6845(MemoryController, shutdownCts.Token);
      RegisterInternalDevice(new DMAController8237());
      RegisterInternalDevice(Pic);
      RegisterInternalDevice(Pit);
      RegisterInternalDevice(new ProgrammablePeripheralInterface8255(this, shutdownCts.Token));
      RegisterInternalDevice(new CMOSRealTimeClock());
      RegisterInternalDevice(Graphics);
      //RegisterInternalDevice(new ParallelPort());
      //RegisterInternalDevice(new FloppyDiskController8272());
      //RegisterInternalDevice(new SerialPort8250());
    }

    private void RegisterInternalDevice(IInternalDevice device)
    {
      foreach (var port in device.PortNumbers)
      {
        Ports[port] = device;
      }
    }
  }
}