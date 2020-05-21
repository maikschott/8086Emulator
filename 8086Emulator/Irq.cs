namespace Masch.Emulator8086
{
  public enum Irq : byte
  {
    Timer,
    Keyboard,
    Secondary, // IRQ 8-15
    ComEven,
    ComOdd,
    Lpt2,
    FloppyDevice,
    Lpt1,
    RealTimeClock,
    MathProcessor = 13,
    PrimaryHardDisk,
    SecondaryHardDisk
  }
}