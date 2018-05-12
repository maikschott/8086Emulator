namespace Masch._8086Emulator
{
  public static class SpecialOffset
  {
    public const int InterruptVectorTable = 0x00000;

    public const int BiosDataArea = 0x00400;

    public const int ApplicationMemory = BiosDataArea + 256;

    public const int Graphics = 0xA0000;

    public const int MonochromeText = 0xB0000;

    public const int ColorText = 0xB8000;

    public const int Rom = 0xC0000;

    public const int BootStrapping = 0xFFFF0;

    public const int ExtendedMemory = 0x100000;
  }
}
