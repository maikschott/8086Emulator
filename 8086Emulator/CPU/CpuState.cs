using System.Diagnostics.CodeAnalysis;

namespace Masch.Emulator8086.CPU
{
  [SuppressMessage("ReSharper", "InconsistentNaming")]
  public class CpuState
  {
    public bool CarryFlag, ParityFlag, AuxiliaryCarryFlag, ZeroFlag, SignFlag;
    public bool TrapFlag, InterruptEnableFlag, DirectionFlag, OverflowFlag;

    public bool BelowOrEqual => CarryFlag | ZeroFlag;
    public bool Less => SignFlag ^ OverflowFlag;
    public bool LessOrEqual => Less | ZeroFlag;

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
    public ushort CS;

    /// <summary>Data segment</summary>
    public ushort DS;

    /// <summary>Stack segment</summary>
    public ushort SS;

    /// <summary>Extra segment</summary>
    public ushort ES;

    protected byte GetRegister8(int index)
    {
      var regValue = Registers[index & 0b11];
      if ((index & 0x4) != 0) { regValue >>= 8; }
      return (byte)regValue;
    }

    protected void SetRegister8(int index, byte value)
    {
      ref var regValue = ref Registers[index & 0b11];
      if ((index & 0x4) == 0) // Lo
      {
        regValue = (ushort)((regValue & 0xFF00) | value);
      }
      else // Hi
      {
        regValue = (ushort)((regValue & 0x00FF) | (value << 8));
      }
    }

    protected ushort GetFlags()
    {
      ushort flags = 0xF000;
      if (CarryFlag) { flags |= 0x01; }
      if (ParityFlag) { flags |= 0x04; }
      if (AuxiliaryCarryFlag) { flags |= 0x10; }
      if (ZeroFlag) { flags |= 0x40; }
      if (SignFlag) { flags |= 0x80; }
      if (TrapFlag) { flags |= 0x100; }
      if (InterruptEnableFlag) { flags |= 0x200; }
      if (DirectionFlag) { flags |= 0x400; }
      if (OverflowFlag) { flags |= 0x800; }
      return flags;
    }

    protected void SetFlags(byte flags)
    {
      CarryFlag = (flags & 0x01) != 0;
      ParityFlag = (flags & 0x04) != 0;
      AuxiliaryCarryFlag = (flags & 0x10) != 0;
      ZeroFlag = (flags & 0x40) != 0;
      SignFlag = (flags & 0x80) != 0;
    }

    protected void SetFlags(ushort flags)
    {
      SetFlags((byte)flags);
      TrapFlag = (flags & 0x100) != 0;
      InterruptEnableFlag = (flags & 0x200) != 0;
      DirectionFlag = (flags & 0x400) != 0;
      OverflowFlag = (flags & 0x800) != 0;
    }
  }
}