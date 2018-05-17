using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Masch._8086Emulator.InternalDevices;

namespace Masch._8086Emulator
{
  public class Machine
  {
    private CancellationTokenSource rebootCts;

    public Machine()
    {
      MemoryController = new MemoryController();
      Cpu = new Cpu(this);
      Ports = new Dictionary<int, IInternalDevice>();

      Reboot();
    }

    public Cpu Cpu { get; }
    public MemoryController MemoryController { get; }
    public GraphicController Graphics { get; private set; }
    public ProgrammableInterruptController8259 Pic { get; private set; }
    public ProgrammableInterruptTimer8253 Pit { get; private set; }

    public Dictionary<int, IInternalDevice> Ports { get; }
    public CancellationToken RebootCancellationToken => rebootCts.Token;

    public bool Running { get; set; }

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

      Array.Copy(bytes, 0, MemoryController.Memory, segment << 4, bytes.Length);
    }

    public void Reboot()
    {
      rebootCts?.Cancel();
      rebootCts = new CancellationTokenSource();
      
      Cpu.Reset();
      Ports.Clear();
      ConnectInternalDevices();
    }

    public void Run()
    {
      Running = true;
      var watch = Stopwatch.StartNew();
      var opcodeCount = 0;
      while (Running)
      {
        Cpu.Tick();
        opcodeCount++;
        Thread.Sleep(0);
      }
      watch.Stop();
      Console.WriteLine($"\r\nProcessed {opcodeCount:N0} opcodes in {watch.Elapsed} ({opcodeCount / watch.Elapsed.TotalSeconds:N0} op/s, {Cpu.ClockCount / watch.Elapsed.TotalSeconds / 1024 / 1024:F1} MHz)");
    }

    private void ConnectInternalDevices()
    {
      Pic = new ProgrammableInterruptController8259();
      Pit = new ProgrammableInterruptTimer8253(Pic);
      Graphics = new GraphicController(MemoryController);
      RegisterInternalDevice(new DMAController8237());
      RegisterInternalDevice(Pic);
      RegisterInternalDevice(Pit);
      RegisterInternalDevice(new ProgrammablePeripheralInterface8255());
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