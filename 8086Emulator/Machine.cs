using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Masch._8086Emulator.Ports;

namespace Masch._8086Emulator
{
  public class Machine
  {
    public Machine()
    {
      MemoryController = new MemoryController();
      Cpu = new Cpu(this);
      Graphics = new Graphics(MemoryController);
      Ports = new Dictionary<int, IPort>();

      ConnectPorts();
    }

    public Cpu Cpu { get; }
    public MemoryController MemoryController { get; }
    public Graphics Graphics { get; }
    public Dictionary<int, IPort> Ports { get; }

    public bool Running { get; set; }

    public void LoadBootstrapper(ushort segment, byte[] bytes)
    {
      LoadProgram(segment, bytes);

      var ofs = SpecialOffset.BootStrapping;
      MemoryController.WriteByte(ofs++, 0xEA); // JMP FAR
      MemoryController.WriteWord(ofs += 2, 0); // offset
      MemoryController.WriteWord(ofs, segment); // segment
    }

    public void LoadProgram(ushort segment, byte[] bytes, int stackSize = 0x100)
    {
      Array.Copy(bytes, 0, MemoryController.Memory, segment * 16, bytes.Length);
      Cpu.SP = (ushort)stackSize;
    }

    public void Reboot()
    {
      Cpu.Reset();
    }

    public void Run()
    {
      Running = true;
      var watch = Stopwatch.StartNew();
      var opcodeCount = 0;
      while (Running)
      {
        Cpu.Cycle();
        opcodeCount++;
        Thread.Sleep(0);
      }
      watch.Stop();
      Console.WriteLine($"\r\nProcessed {opcodeCount} opcodes in {watch.Elapsed} ({opcodeCount / watch.Elapsed.TotalSeconds:N0} op/s)");
    }

    private void ConnectPorts()
    {
      //RegisterController<DMAController8237>();
      //RegisterController<ProgrammableInterruptController8259>();
      //RegisterController<ProgrammableInterruptTimer8253>();
      //RegisterController<GraphicController>();
      //RegisterController<ParallelPort>();
      //RegisterController<FloppyDiskController>();
      //RegisterController<SerialPort>();
    }

    private void RegisterController<T>() where T : IPort, new()
    {
      var controller = new T();
      foreach (var port in controller.PortNumbers)
      {
        Ports[port] = controller;
      }
    }
  }
}