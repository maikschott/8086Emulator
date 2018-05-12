using System;
using System.Diagnostics.CodeAnalysis;

namespace Masch._8086Emulator
{
  [Flags]
  public enum CpuFlags : ushort
  {
    Carry = 0x1,
    Parity = 0x4,
    AuxiliaryCarry = 0x10,
    Zero = 0x40,
    Sign = 0x80,
    Trap = 0x100,
    InterruptEnable = 0x200,
    Direction = 0x400,
    Overflow = 0x800,
    AlwaysOn = 0xF000
  }

  [SuppressMessage("ReSharper", "InconsistentNaming")]
  public class CpuState
  {
    public CpuFlags Flags { get; set; } = CpuFlags.AlwaysOn;

    public bool BelowOrEqual => Flags.HasFlag(CpuFlags.Carry) | Flags.HasFlag(CpuFlags.Zero);
    public bool Less => Flags.HasFlag(CpuFlags.Sign) ^ Flags.HasFlag(CpuFlags.Overflow);
    public bool LessOrEqual => Less | Flags.HasFlag(CpuFlags.Zero);

    public ushort[] Registers = new ushort[8];
    public string[] RegisterNames = { nameof(AX), nameof(CX), nameof(DX), nameof(BX), nameof(SP), nameof(BP), nameof(SI), nameof(DI) };
    public string[] RegisterNames8 = { nameof(AL), nameof(CL), nameof(DL), nameof(BL), nameof(AH), nameof(CH), nameof(DH), nameof(BH) };

    /// <summary>Instruction pointer</summary>
    public ushort IP;

    /// <summary>Accumulator register</summary>
    public ref ushort AX => ref Registers[0];

    /// <summary>Counter register</summary>
    public ref ushort CX => ref Registers[1];

    /// <summary>Data register</summary>
    public ref ushort DX => ref Registers[2];

    /// <summary>Base register</summary>
    public ref ushort BX => ref Registers[3];

    /// <summary>Stack pointer</summary>
    public ref ushort SP => ref Registers[4];

    /// <summary>Base pointer</summary>
    public ref ushort BP => ref Registers[5];

    /// <summary>Source index</summary>
    public ref ushort SI => ref Registers[6];

    /// <summary>Destination index</summary>
    public ref ushort DI => ref Registers[7];

    public byte AH
    {
      get => (byte)(AX >> 8);
      set => AX = (ushort)((value << 8) | AL);
    }

    public byte CH
    {
      get => (byte)(CX >> 8);
      set => CX = (ushort)((value << 8) | CL);
    }

    public byte DH
    {
      get => (byte)(DX >> 8);
      set => DX = (ushort)((value << 8) | DL);
    }

    public byte BH
    {
      get => (byte)(BX >> 8);
      set => DX = (ushort)((value << 8) | DL);
    }

    public byte AL
    {
      get => (byte)AX;
      set => AX = (ushort)((AH << 8) | value);
    }

    public byte CL
    {
      get => (byte)CX;
      set => CX = (ushort)((CH << 8) | value);
    }

    public byte DL
    {
      get => (byte)DX;
      set => DX = (ushort)((DH << 8) | value);
    }

    public byte BL
    {
      get => (byte)BX;
      set => DX = (ushort)((BH << 8) | value);
    }

    /// <summary>Code segment</summary>
    public ushort CS { get; set; }

    /// <summary>Data segment</summary>
    public ushort DS { get; set; }

    /// <summary>Stack segment</summary>
    public ushort SS { get; set; }

    /// <summary>Extra segment</summary>
    public ushort ES { get; set; }

    protected byte GetRegister8(int index)
    {
      var regValue = Registers[index & 0x3];
      if ((index & 0x4) != 0) { regValue >>= 8; }
      return (byte)regValue;
    }

    protected void SetRegister8(int index, byte value)
    {
      ref var regValue = ref Registers[index & 0x3];
      if ((index & 0x4) == 0) // Lo
      {
        regValue = (ushort)((regValue & 0xFF00) | value);
      }
      else // Hi
      {
        regValue = (ushort)((regValue & 0x00FF) | (value << 8));
      }
    }
  }
}