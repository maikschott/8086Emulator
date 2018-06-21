using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Masch.Emulator8086.InternalDevices
{
  [SuppressMessage("ReSharper", "InconsistentNaming")]
  public enum VideoMode : byte
  {
    Text40x25Monochrome,
    Text40x25Color,
    Text80x25Gray,
    Text80x25Color,
    Graphics320x200x4Color,
    Graphics320x200x4Color2,
    Graphics640x200Monochrome,
    Text80x25TextMonochrome,
    Graphics160x200x16ColorPcjr,
    Graphics320x200x16ColorPcjr,
    Graphics320x200x16Color = 0x0D,
    Graphics640x200x16Color,
    Graphics640x350Monochrome,
    Graphics640x350x16Colors,
    Graphics640x480Monochrome,
    Graphics640x480x16Colors,
    Graphics320x240x256Colors
  }

  // see http://stanislavs.org/helppc/6845.html
  public class CrtController6845 : IInternalDevice
  {
    private const int StdOutputHandle = -11;
    private const int RegisterCursorStart = 0x0A;
    private const int RegisterCursorEnd = 0x0B;
    private const int RegisterPageStartOfsHi = 0x0C;
    private const int RegisterPageStartOfsLo = 0x0D;
    private const int RegisterCursorAddrHi = 0x0E;
    private const int RegisterCursorAddrLo = 0x0F;
    private const int BlockHeight = 8;
    public static int TextStartOfs = SpecialOffset.ColorText;

    //private static readonly int[] mdaPorts = Enumerable.Range(0x3B0, 13).ToArray();
    //private static readonly int[] egaPorts = Enumerable.Range(0x3C0, 16).ToArray();
    private static readonly int[] cgaPorts = Enumerable.Range(0x3D0, 12).ToArray();

    private static readonly TimeSpan refreshDelay = TimeSpan.FromSeconds(1 / 60d); // 60 Hz

    private readonly IntPtr consoleHandle;
    private readonly MemoryController memoryController;
    private readonly byte[] registers;
    private short[] buffer;
    private int colors;
    private int columns;
    private Action fillTextBuffer;
    private bool graphicMode;
    private int pageOfs;
    private byte registerIndex, stateIndex;
    private int rows;
    private bool cursorPositionChanged;

#if NETCOREAPP
    private readonly bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
#else
    private const bool isWindows = true;
#endif

    public CrtController6845(MemoryController memoryController, CancellationToken shutdownCancellationToken)
    {
      this.memoryController = memoryController;
      registers = new byte[0x12];

      ChangeResolution(TextStartOfs == SpecialOffset.ColorText ? VideoMode.Text80x25Color : VideoMode.Text80x25TextMonochrome);
      if (isWindows)
      {
        SetConsoleOutputCP(437);
        consoleHandle = GetStdHandle(StdOutputHandle);
      }

      // task to copy the text video memory to the console buffer
      Task.Run(async () =>
      {
        while (!shutdownCancellationToken.IsCancellationRequested)
        {
          var task = Task.Delay(refreshDelay, shutdownCancellationToken);
          if (isWindows)
          {
            CopyMemoryToConsoleBuffer();
          }
          else
          {
            WriteMemoryToConsole();
          }

          if (cursorPositionChanged)
          {
            cursorPositionChanged = false;
            SetCursorPosition((registers[RegisterCursorAddrHi] << 8) | registers[RegisterCursorAddrLo]);
          }

          await task.ConfigureAwait(false);
        }
      });
    }

    public IEnumerable<int> PortNumbers =>
      /*mdaPorts
      .Append(HerculesConfigurationSwitchRegister)
      .Concat(egaPorts)
      .Concat*/cgaPorts;

    public byte GetByte(int port)
    {
      if ((port & 0xFFF0) == 0x03B0) { port += 0x20; }

      switch (port)
      {
        case 0x3D1:
        case 0x3D3:
        case 0x3D5:
        case 0x3D7:
          if (registerIndex < 0x10) { return registers[registerIndex]; }
          break;
        case 0x3D8:
          return 0b0000_0001;
        case 0x3DA:
          stateIndex = (byte)(++stateIndex % 4);
          switch (stateIndex)
          {
            case 0:
              return 0b1000;
            case 1:
            case 3:
              return 0b0000;
            case 2:
              return 0b0001;
          }
          break;
      }

      return 0;
    }

    public void SetByte(int port, byte value)
    {
      if ((port & 0xFFF8) == 0x03B0) { port += 0x20; } // 0x3B0-0x3B7 is mapped to 0x3D0-0x3D7

      switch (port)
      {
        case 0x3D0:
        case 0x3D2:
        case 0x3D4:
        case 0x3D6:
          registerIndex = value;
          break;
        case 0x3D1:
        case 0x3D3:
        case 0x3D5:
        case 0x3D7:
          if (registerIndex < 0x10)
          {
            registers[registerIndex] = value;
            switch (registerIndex)
            {
              case RegisterCursorStart:
              case RegisterCursorEnd:
                ChangeCursorSize(registers[RegisterCursorStart], registers[RegisterCursorEnd]);
                break;
              case RegisterPageStartOfsHi:
                pageOfs = (value << 8) | (byte)pageOfs;
                break;
              case RegisterPageStartOfsLo:
                pageOfs = (pageOfs & 0xFF00) | value;
                break;
              //case RegisterCursorAddrHi:
              case RegisterCursorAddrLo:
                cursorPositionChanged = true;
                break;
            }
          }
          break;
        case 0x3D8: // CGA mode control register
          break;
      }
    }

    private void ChangeCursorSize(byte cursorStart, byte cursorEnd)
    {
      if (cursorStart > cursorEnd || !isWindows) { return; }
      Console.CursorSize = 100 * (cursorEnd - cursorStart + 1) / BlockHeight;
    }

    private void ChangeResolution(VideoMode videoMode)
    {
      (columns, rows, graphicMode, colors) = GetVideoParameters(videoMode);
      if (graphicMode) { return; }

      if (colors == 2) { fillTextBuffer = FillTextBuffer02; }
      else if (colors == 16) { fillTextBuffer = FillTextBuffer16; }

      buffer = new short[rows * columns * 2];
      if (isWindows)
      {
        Console.SetWindowSize(columns, rows);
        Console.BufferWidth = columns;
      }
    }

    private void CopyMemoryToConsoleBuffer()
    {
      fillTextBuffer();
      var bufferWidth = new Coord { X = (short)columns, Y = (short)rows };
      var bufferStart = Coord.Zero;
      var writeRegion = new SmallRect { Left = bufferStart.X, Top = bufferStart.Y, Right = (short)(bufferWidth.X - 1), Bottom = (short)(bufferWidth.Y - 1) };
      WriteConsoleOutput(consoleHandle, buffer, bufferWidth, bufferStart, ref writeRegion);
    }

    private int CalcChecksum(short[] bytes)
    {
      var sum = 0;
      unchecked
      {
        for (int i = 0; i < bytes.Length; i++) sum += bytes[i];
      }
      return sum;
    }

    private void WriteMemoryToConsole()
    {
      var oldCheckSum = CalcChecksum(buffer);
      fillTextBuffer();
      var newCheckSum = CalcChecksum(buffer);
      if (oldCheckSum == newCheckSum) { return; }

      var oldCursorX = Console.CursorLeft;
      var oldCursorY = Console.CursorTop;

      byte? oldColor = null;
      var i = 0;
      for (var y = 0; y < rows; y++)
      {
        Console.SetCursorPosition(0, y);
        for (var x = 0; x < columns; x++)
        {
          var color = (byte)buffer[i + 1];
          if (oldColor != color)
          {
            Console.BackgroundColor = (ConsoleColor)(color >> 4);
            Console.ForegroundColor = (ConsoleColor)(color & 0xF);
            oldColor = color;
          }

          var ch = (char)buffer[i];
          if (ch >= 128) { ch = '\uFFFD'; } // Encoding 437 not supported on Linux and NuGet package System.Text.Encoding.CodePages won't load on startup
          Console.Write(ch);
          i += 2;
        }
      }

      Console.SetCursorPosition(oldCursorX, oldCursorY);
    }

    private void FillTextBuffer02()
    {
      for (var i = 0; i < columns * rows; i++)
      {
        buffer[i * 2 + 0] = memoryController.ReadByte(TextStartOfs + pageOfs + i);
        buffer[i * 2 + 1] = (byte)ConsoleColor.Gray;
      }
    }

    private void FillTextBuffer16()
    {
      memoryController.ReadBlock(TextStartOfs + pageOfs, buffer, buffer.Length);
    }

    private static (int columns, int rows, bool graphics, int colors) GetVideoParameters(VideoMode value)
    {
      switch (value)
      {
        case VideoMode.Text40x25Monochrome:
          return (40, 25, false, 2);
        case VideoMode.Text40x25Color:
          return (40, 25, false, 16);
        case VideoMode.Text80x25Gray:
        case VideoMode.Text80x25Color:
          return (80, 25, false, 16);
        case VideoMode.Text80x25TextMonochrome:
          return (80, 25, false, 2);
        case VideoMode.Graphics160x200x16ColorPcjr:
          return (160, 200, true, 16);
        case VideoMode.Graphics320x200x4Color:
        case VideoMode.Graphics320x200x4Color2:
          return (320, 200, true, 4);
        case VideoMode.Graphics320x200x16ColorPcjr:
        case VideoMode.Graphics320x200x16Color:
          return (320, 200, true, 16);
        case VideoMode.Graphics320x240x256Colors:
          return (320, 200, true, 256);
        case VideoMode.Graphics640x200Monochrome:
          return (640, 200, true, 2);
        case VideoMode.Graphics640x200x16Color:
          return (640, 200, true, 16);
        case VideoMode.Graphics640x350Monochrome:
          return (640, 350, true, 2);
        case VideoMode.Graphics640x350x16Colors:
          return (640, 350, true, 16);
        case VideoMode.Graphics640x480Monochrome:
          return (640, 480, true, 2);
        case VideoMode.Graphics640x480x16Colors:
          return (640, 480, true, 16);
        default:
          return default;
      }
    }

    private void SetCursorPosition(int cursorAddr)
    {
      var row = Math.DivRem(cursorAddr, columns, out var col);
      if (row >= rows) { row = rows - 1; }
      Console.SetCursorPosition(col, row);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Coord
    {
      public static readonly Coord Zero;

      public short X;
      public short Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SmallRect
    {
      public short Left;
      public short Top;
      public short Right;
      public short Bottom;
    }

    #region P/Invoke

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll")]
    private static extern bool SetConsoleOutputCP(int codePage);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
    private static extern bool WriteConsoleOutput(IntPtr hConsoleOutput, short[] lpBuffer, Coord dwBufferSize, Coord dwBufferCoord, ref SmallRect lpWriteRegion);

    #endregion
  }
}