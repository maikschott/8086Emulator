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
    DoubleFault,
    CoprocessorSegmentOverrun,
    // ReSharper disable once InconsistentNaming
    InvalidTSS,
    SegmentNotPresent,
    StackSegmentFault,
    GeneralProtectionFault,
    PageFault,
    FloatingPointException = 16,
    AlignmentCheck,
    MachineCheck
  }
}
