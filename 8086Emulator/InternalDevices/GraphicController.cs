using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;

namespace Masch._8086Emulator.InternalDevices
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
  public class GraphicController : IInternalDevice
  {
    public static int TextStartOfs = SpecialOffset.ColorText;

    private static readonly int[] mdaPorts = Enumerable.Range(0x3B0, 13).ToArray();
    private static readonly int[] egaPorts = Enumerable.Range(0x3C0, 16).ToArray();
    private static readonly int[] cgaPorts = Enumerable.Range(0x3D0, 12).ToArray();
    private readonly char[] charMapping;
    private readonly MemoryController memoryController;
    private readonly byte[] registers;
    private int colors;
    private int columns;
    private bool graphicMode;
    private byte registerIndex, stateIndex;
    private int rows;

    public GraphicController(MemoryController memoryController)
    {
      this.memoryController = memoryController;

      charMapping = Encoding.GetEncoding(437).GetChars(Enumerable.Range(0, 256).Select(x => (byte)x).ToArray());
      registers = new byte[0x12];

      memoryController.RegisterChangeNotifier(TextStartOfs >> 4, (TextStartOfs >> 4) + 0x100, TextMemoryChanged);

      ChangeResolution(TextStartOfs == SpecialOffset.ColorText ? VideoMode.Text80x25Color : VideoMode.Text80x25TextMonochrome);
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
        case 0x3DA:
          stateIndex = (byte)((++stateIndex) % 4);
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
        case 0x3D8:
          return 0b0000_0001;
      }

      return 0;
    }

    public void SetByte(int port, byte value)
    {
      if ((port & 0xFFF0) == 0x03B0) { port += 0x20; }

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
          if (registerIndex <= 0x0F)
          {
            registers[registerIndex] = value;
          }
          break;
      }
    }

    private void ChangeResolution(VideoMode videoMode)
    {
      (columns, rows, graphicMode, colors) = GetResolution(videoMode);
      if (graphicMode) { return; }

      Console.SetWindowSize(columns, rows + 1);
      Console.BufferWidth = columns;
    }

    private static (int columns, int rows, bool graphics, int colors) GetResolution(VideoMode value)
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
          return (0, 0, false, 0);
      }
    }

    // maybe replace it with a task running at refresh rate
    private void TextMemoryChanged(int offset, byte value)
    {
      var actualOffset = offset;
      offset -= TextStartOfs;
      if (colors == 16)
      {
        var isAttribute = (offset & 0b1) != 0;
        offset >>= 1;
        var y = offset / columns;
        if (y >= rows) { return; }
        var x = offset % columns;
        Console.SetCursorPosition(x, y);
        if (isAttribute)
        {
          Console.BackgroundColor = (ConsoleColor)(value >> 4);
          Console.ForegroundColor = (ConsoleColor)(value & 0x0F);
          value = memoryController.ReadByte(actualOffset - 1);
        }
        else
        {
          Console.BackgroundColor = ConsoleColor.Black;
          Console.ForegroundColor = ConsoleColor.Gray;
        }
        Console.Write(charMapping[value]);
      }
      else
      {
        var y = offset / columns;
        if (y >= rows) { return; }
        var x = offset % columns;
        Console.SetCursorPosition(x, y);
        Console.Write(charMapping[value]);
      }
    }
  }
}