using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Masch._8086Emulator.InternalDevices
{
  public class ProgrammablePeripheralInterface8255 : IInternalDevice
  {
    private const byte SenseInfo = 0x2C;
    private const int VK_LSHIFT = 0xA0;
    private const int VK_RSHIFT = 0xA1;
    private const int VK_LCONTROL = 0xA2;
    private const int VK_RCONTROL = 0xA3;
    private const int VK_LMENU = 0xA4; // Alt left
    private const int VK_RMENU = 0xA5; // Alt right
    private readonly byte[] data = new byte[4];
    private readonly ProgrammableInterruptController8259 pic;
    private readonly ProgrammableInterruptTimer8253 pit;
    private bool resetRequested;

    public ProgrammablePeripheralInterface8255(Machine machine, CancellationToken shutdownCancellationToken)
    {
      pic = machine.Pic;
      pit = machine.Pit;
      // IBM PC BIOS fetches the LSB of the Equipment Word from Port 0x60.
      // - Bits 5-4: 0=EGA, 1=CGA 40x25, 2=CGA 80x25, 3=MDA
      // - Bits 3-2=(value + 4) << 12 is memory size (undocumented, but used during POST).
      //   We return the max (64K) although we have much more.
      data[0] = SenseInfo;

      Console.TreatControlCAsInput = true;
      Task.Run(async () =>
      {
        var scanCodes = Enumerable.Range(0, 256).Select(x => (byte)MapVirtualKey(x, 0)).ToArray();

        while (!shutdownCancellationToken.IsCancellationRequested)
        {
          await Task.Delay(50, shutdownCancellationToken).ConfigureAwait(false);

          if (!Console.KeyAvailable) { continue; }
          var consoleKeyInfo = Console.ReadKey(true);

          if (consoleKeyInfo.Modifiers == (ConsoleModifiers.Control | ConsoleModifiers.Alt))
          {
            if (consoleKeyInfo.Key == ConsoleKey.C) { machine.Stop(); }
            else if (consoleKeyInfo.Key == ConsoleKey.M)
            {
              var memory = new byte[SpecialOffset.HighMemoryArea];
              machine.MemoryController.ReadBlock(0, memory, memory.Length);
              File.WriteAllBytes(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Memory.bin"), memory);
            }
          }

          if (consoleKeyInfo.Modifiers.HasFlag(ConsoleModifiers.Shift))
          {
            PutKey(scanCodes[VK_LSHIFT]);
            await Task.Delay(20, shutdownCancellationToken).ConfigureAwait(false);
          }
          PutKey(scanCodes[(byte)consoleKeyInfo.Key]);
          if (consoleKeyInfo.Modifiers.HasFlag(ConsoleModifiers.Shift))
          {
            await Task.Delay(20, shutdownCancellationToken).ConfigureAwait(false);
            PutKey((byte)(scanCodes[VK_LSHIFT] | 0x80));
          }
        }
      });
    }

    public IEnumerable<int> PortNumbers => Enumerable.Range(0x60, 4);

    public byte GetByte(int port)
    {
      var result = data[port & 0b11];

      if (resetRequested && port == 0x60)
      {
        data[0] = SenseInfo;
        resetRequested = false;
      }

      return result;
    }

    public void SetByte(int port, byte value)
    {
      data[port & 0b11] = value;

      // used in IBM PC at BIOS POST, which does the following sequence to reset the keyboard:
      // - send 0x0C: SET KBD CLK LINE LOW
      // - wait 20ms
      // - send 0xCC: SET CLK, ENABLE LINES HIGH
      // - send 0x4C: SET KBD CLK HIGH, ENABLE LOW
      // - keyboard will reset and respond with scancode 0xAA
      if (port == 0x61 && value == 0x4C)
      {
        resetRequested = true;
        pit.PostAction(() => PutKey(0xAA)); // reset scancode
      }
    }

    [DllImport("user32.dll")]
    private static extern int MapVirtualKey(int code, int mapType);

    private void PutKey(byte scanCode)
    {
      if ((scanCode & 0x7F) == 0) { return; }

      data[0] = scanCode;
      pic.Invoke(Irq.Keyboard);
    }
  }
}