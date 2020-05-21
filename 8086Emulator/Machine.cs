#define _SPEEDLIMIT
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Masch.Emulator8086.CPU;
using Masch.Emulator8086.InternalDevices;

namespace Masch.Emulator8086
{
  public class Machine
  {
    private readonly Cpu cpu;
    private readonly MemoryController memoryController;
    private readonly EventToken eventToken;
    private readonly DeviceManager deviceManager;
    private bool running;

    public Machine(Cpu cpu, MemoryController memoryController, EventToken eventToken, DeviceManager deviceManager)
    {
      this.cpu = cpu;
      this.memoryController = memoryController;
      this.eventToken = eventToken;
      this.deviceManager = deviceManager;
    }

    public void LoadAndSetBootstrapper(int segment, byte[] bytes)
    {
      LoadProgram(segment, bytes);

      var ofs = SpecialOffset.BootStrapping;
      memoryController.WriteByte(ofs++, 0xEA); // JMP FAR
      memoryController.WriteWord(ofs += 2, 0); // offset
      memoryController.WriteWord(ofs, (ushort)segment); // segment
    }

    public void LoadProgram(int segment, byte[] bytes)
    {
      if (segment < 0 || segment > ushort.MaxValue) { throw new ArgumentOutOfRangeException(nameof(segment)); }
      if (bytes == null) { throw new ArgumentNullException(nameof(bytes)); }

      memoryController.WriteBlock(segment << 4, bytes, bytes.Length);
    }

    public void Reboot()
    {
      eventToken.ShutDown.Cancel();
      eventToken.ShutDown = new CancellationTokenSource();
      eventToken.Halt = new CancellationTokenSource();

      cpu.Reset();
    }

    public async Task RunAsync()
    {
      running = true;
      var watch = Stopwatch.StartNew();
      var opcodeCount = 0;

#if SPEEDLIMIT
      var lastElapsed = watch.Elapsed;
      var lastClockCount = Cpu.ClockCount;
      var measurementTime = TimeSpan.FromMilliseconds(10);
#endif

      await using (eventToken.Halt.Token.Register(() => running = false))
      {
        while (running)
        {
          cpu.Tick();
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
      }

      eventToken.ShutDown.Cancel();

      await Task.WhenAll(deviceManager.Select(x => x.Task).ToArray()).ContinueWith(task => {}, TaskContinuationOptions.OnlyOnCanceled);

      watch.Stop();
      Console.WriteLine($"\r\nProcessed {opcodeCount:N0} opcodes in {watch.Elapsed} ({opcodeCount / watch.Elapsed.TotalSeconds:N0} op/s, {cpu.ClockCount / watch.Elapsed.TotalSeconds / 1024 / 1024:F2} MHz)");
    }
  }
}