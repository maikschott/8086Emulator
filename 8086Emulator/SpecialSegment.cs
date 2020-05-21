namespace Masch.Emulator8086
{
  public static class SpecialOffset
  {
    public const int InterruptVectorTable = 0x00000;

    public const int BiosDataArea = 0x00400;

    public const int ExtendedVideoMemory = 0xA0000;

    public const int StandardVideoMemory = 0xB0000;

    public const int ColorText = 0xB8000;

    public const int VideoBios = 0xC0000;

    public const int HardDiskBios = 0xC8000;

    public const int Bios = 0xF0000;

    public const int BootStrapping = 0xFFFF0;

    public const int UpperMemoryArea = 640 * 1024;
    public const int HighMemoryArea = 1024 * 1024;
  }
}