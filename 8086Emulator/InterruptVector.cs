namespace Masch._8086Emulator
{
  public enum InterruptVector : byte
  {
    DivideByZero,
    Debug,
    NonMaskable,
    Breakpoint,
    Overflow,
    BoundRangeExceeded,
    InvalidOpcode,
    DeviceNotAvailable,
    FloatingPointException = 16,
    AlignmentCheck,
    MachineCheck
  }
}
