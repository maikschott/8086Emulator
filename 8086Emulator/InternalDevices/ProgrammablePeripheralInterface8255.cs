﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Masch.Emulator8086.InternalDevices
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
    private readonly bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    private readonly ILogger<ProgrammablePeripheralInterface8255> logger;
    private readonly ProgrammableInterruptController8259 pic;
    private readonly ProgrammableInterruptTimer8253 pit;
    private bool resetRequested;

    public ProgrammablePeripheralInterface8255(ILogger<ProgrammablePeripheralInterface8255> logger,
      EventToken eventToken,
      MemoryController memoryController,
      ProgrammableInterruptTimer8253 pit,
      ProgrammableInterruptController8259 pic)
    {
      this.logger = logger;
      this.pic = pic;
      this.pit = pit;
      // IBM PC BIOS fetches the LSB of the Equipment Word from Port 0x60.
      // - Bits 5-4: 0=EGA, 1=CGA 40x25, 2=CGA 80x25, 3=MDA
      // - Bits 3-2=(value + 4) << 12 is memory size (undocumented, but used during POST).
      //   We return the max (64K) although we have much more.
      data[0] = SenseInfo;
      Console.TreatControlCAsInput = true;

      var shutdownCancellationToken = eventToken.ShutDown.Token;

      Task = Task.Run(async () =>
      {
        byte[] scanCodes;
        if (isWindows)
        {
          scanCodes = Enumerable.Range(0, 256).Select(x => (byte)MapVirtualKey(x, 0)).ToArray();
        }
        else
        {
          Dictionary<int, int> linuxScanCodes = ReadLinuxScanCodes();
          scanCodes = Enumerable.Range(0, 256)
            .Select(x => linuxScanCodes.TryGetValue(x, out var scanCode) ? (byte)scanCode : (byte)0).ToArray();
        }

        while (!shutdownCancellationToken.IsCancellationRequested)
        {
          await Task.Delay(50, shutdownCancellationToken).ConfigureAwait(false);

          if (!Console.KeyAvailable) { continue; }

          var consoleKeyInfo = Console.ReadKey(true);

          if (consoleKeyInfo.Modifiers == (ConsoleModifiers.Control | ConsoleModifiers.Alt))
          {
            if (consoleKeyInfo.Key == ConsoleKey.C) { eventToken.Halt.Cancel(); }
            else if (consoleKeyInfo.Key == ConsoleKey.M)
            {
              await File.WriteAllBytesAsync(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Memory.bin"),
                memoryController.Memory, shutdownCancellationToken);
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

    public Task Task { get; }

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

    private Dictionary<int, int> ReadLinuxScanCodes()
    {
      var result = new Dictionary<int, int>();

      var keyCodes = Enum.GetValues(typeof(ConsoleKey)).Cast<ConsoleKey>()
        .ToDictionary<ConsoleKey, string, int>(x => x.ToString(), x => (int)x, StringComparer.OrdinalIgnoreCase);
      keyCodes.Add("LeftShift", VK_LSHIFT);
      keyCodes.Add("RightShift", VK_RSHIFT);
      keyCodes.Add("LeftCtrl", VK_LCONTROL);
      keyCodes.Add("RightCtrl", VK_RCONTROL);
      keyCodes.Add("LeftAlt", VK_LMENU);
      keyCodes.Add("RightAlt", VK_RMENU);

      try
      {
        var lines = File.ReadAllLines(@"/usr/include/linux/input-event-codes.h");
        var regex = new Regex(@"^#define\s+KEY_(.+?)\s+(\d+)");
        foreach (var line in lines)
        {
          var match = regex.Match(line);
          if (!match.Success) { continue; }

          var keyName = match.Groups[1].Value;
          var scanCode = int.Parse(match.Groups[2].Value);

          if (int.TryParse(keyName, out _)) { keyName = "D" + keyName; }
          else if (keyName.StartsWith("KP", StringComparison.OrdinalIgnoreCase))
          {
            var numpadKeyName = keyName.Substring(2).ToLowerInvariant();
            switch (numpadKeyName)
            {
              case "0":
              case "1":
              case "2":
              case "3":
              case "4":
              case "5":
              case "6":
              case "7":
              case "8":
              case "9":
                keyName = "NumPad" + numpadKeyName;
                break;
              case "asterisk":
                keyName = ConsoleKey.Multiply.ToString();
                break;
              case "minus":
                keyName = ConsoleKey.Subtract.ToString();
                break;
              case "plus":
                keyName = ConsoleKey.Add.ToString();
                break;
              case "slash":
                keyName = ConsoleKey.Divide.ToString();
                break;
            }
          }
          else if (keyName.Equals("Space", StringComparison.OrdinalIgnoreCase))
          {
            keyName = ConsoleKey.Spacebar.ToString();
          }

          if (keyCodes.TryGetValue(keyName, out var keyCode))
          {
            result.Add(keyCode, scanCode);
          }
        }
      }
      catch (Exception e)
      {
        logger.LogError(e, "Failed to read key code mapping");
      }

      return result;
    }
  }
}