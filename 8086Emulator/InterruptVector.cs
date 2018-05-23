namespace Masch.Emulator8086
{
  public enum InterruptVector : byte
  {
    CpuDivideByZero,
    CpuDebug,
    CpuNonMaskable,
    CpuBreakpoint,
    CpuOverflow,
    CpuBoundRangeExceeded, // 186+
    CpuInvalidOpcode, // 186+
    CpuProcessorExtensionNotAvailable, // 186+
    Irq1, Irq2, Irq3, Irq4, Irq5, Irq6, Irq7, Irq8,
    CpuFloatingPointException, // 286+
    BiosVideo = 0x10,
    BiosEquipment = 0x11,
    BiosMemorySize = 0x12,
    BiosDisketteIo = 0x13,
    BiosRs232Io = 0x14,
    BiosCassetteIo = 0x15,
    BiosKeyboard = 0x16,
    BiosPrinterIo = 0x17,
    BiosBasicInterpreterLoader = 0x18,
    BiosBootstrapLoader = 0x19,
    BiosClock = 0x1A
  }
}
