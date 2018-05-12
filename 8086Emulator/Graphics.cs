using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Masch._8086Emulator
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

  public class Graphics
  {
    public Encoding Encoding;
    public static int TextStartOfs = SpecialOffset.MonochromeText;
    private int columns;
    private int rows;

    public Graphics(MemoryController memoryController)
    {
      (columns, rows) = GetResolution(VideoMode.Text80x25TextMonochrome);
      Encoding = Encoding.GetEncoding("ISO-8859-1");
      memoryController.RegisterChangeNotifier(TextStartOfs >> 4, (TextStartOfs >> 4) + 0x100, TextMemoryChanged);
    }

    private static (int columns, int rows) GetResolution(VideoMode value)
    {
      switch (value)
      {
        case VideoMode.Text40x25Monochrome:
        case VideoMode.Text40x25Color:
          return (40, 25);
        case VideoMode.Text80x25Gray:
        case VideoMode.Text80x25Color:
        case VideoMode.Text80x25TextMonochrome:
          return (80, 25);
        case VideoMode.Graphics160x200x16ColorPcjr:
          return (160, 200);
        case VideoMode.Graphics320x200x4Color:
        case VideoMode.Graphics320x200x4Color2:
        case VideoMode.Graphics320x200x16ColorPcjr:
        case VideoMode.Graphics320x200x16Color:
        case VideoMode.Graphics320x240x256Colors:
          return (320, 200);
        case VideoMode.Graphics640x200Monochrome:
        case VideoMode.Graphics640x200x16Color:
          return (640, 200);
        case VideoMode.Graphics640x350Monochrome:
        case VideoMode.Graphics640x350x16Colors:
          return (640, 350);
        case VideoMode.Graphics640x480Monochrome:
        case VideoMode.Graphics640x480x16Colors:
          return (640, 480);
        default:
          return (0, 0);
      }
    }

    private void TextMemoryChanged(int offset, byte value)
    {
      offset -= TextStartOfs;
      var y = offset / columns;
      var x = offset % columns;
      Console.SetCursorPosition(x, y);
      Console.Write(Encoding.GetChars(new[] { value }));
    }
  }
}