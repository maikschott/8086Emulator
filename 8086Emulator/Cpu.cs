using System;
using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Masch._8086Emulator
{
  public partial class Cpu : CpuState
  {
    private readonly Machine machine;
    private readonly MemoryController memory;

    private readonly BitArray parity;
    private int? currentEffectiveAddress;

    private SegmentRegister dataSegmentRegister;

    private string debugParam;

    // ReSharper disable once InconsistentNaming
    private ushort originalIP;

    private Repeat repeat;

    public Cpu(Machine machine)
    {
      this.machine = machine;
      memory = machine.MemoryController;

      parity = CalcParityTable();
      Reset();
    }

    private ushort DataSegment
    {
      get
      {
        switch (dataSegmentRegister)
        {
          case SegmentRegister.CS:
            return CS;
          case SegmentRegister.ES:
            return ES;
          case SegmentRegister.SS:
            return SS;
          default:
            return DS;
        }
      }
    }

    public void Cycle()
    {
      Debug($"[{CS:X4}:{IP:X4}] ");

      currentEffectiveAddress = null;
      originalIP = IP;
      var opcode = ReadCodeByte();

      switch (opcode)
      {
        case 0x00:
        case 0x01:
        case 0x02:
        case 0x03:
        case 0x04:
        case 0x05:
          Debug("ADD");
          Add(opcode);
          break;
        case 0x06:
          Debug("PUSH ES");
          Push(ES);
          break;
        case 0x07:
          Debug("POP ES");
          ES = Pop();
          break;
        case 0x08:
        case 0x09:
        case 0x0A:
        case 0x0B:
        case 0x0C:
        case 0x0D:
          Debug("OR");
          Or((byte)(opcode - 0x08));
          break;
        case 0x0E:
          Debug("PUSH CS");
          Push(CS);
          break;
        case 0x10:
        case 0x11:
        case 0x12:
        case 0x13:
        case 0x14:
        case 0x15:
          Debug("ADC");
          Adc((byte)(opcode - 0x10));
          break;
        case 0x16:
          Debug("PUSH SS");
          Push(SS);
          break;
        case 0x17:
          Debug("POP SS");
          SS = Pop();
          break;
        case 0x18:
        case 0x19:
        case 0x1A:
        case 0x1B:
        case 0x1C:
        case 0x1D:
          Debug("SBB");
          Sbb((byte)(opcode - 0x18));
          break;
        case 0x1E:
          Debug("PUSH DS");
          Push(DS);
          break;
        case 0x1F:
          Debug("POP DS");
          DS = Pop();
          break;
        case 0x20:
        case 0x21:
        case 0x22:
        case 0x23:
        case 0x24:
        case 0x25:
          Debug("AND");
          And((byte)(opcode - 0x20));
          break;
        case 0x26:
          dataSegmentRegister = SegmentRegister.ES;
          break;
        case 0x27:
          Debug("DAA");
          Daa();
          break;
        case 0x28:
        case 0x29:
        case 0x2A:
        case 0x2B:
        case 0x2C:
        case 0x2D:
          Debug("SUB");
          Sub((byte)(opcode - 0x28));
          break;
        case 0x2E:
          dataSegmentRegister = SegmentRegister.CS;
          break;
        case 0x2F:
          Debug("DAS");
          Das();
          break;
        case 0x30:
        case 0x31:
        case 0x32:
        case 0x33:
        case 0x34:
        case 0x35:
          Debug("XOR");
          Xor((byte)(opcode - 0x30));
          break;
        case 0x36:
          dataSegmentRegister = SegmentRegister.SS;
          break;
        case 0x37:
          Debug("AAA");
          Aaa();
          break;
        case 0x38:
        case 0x39:
        case 0x3A:
        case 0x3B:
        case 0x3C:
        case 0x3D:
          Debug("CMP");
          Cmp((byte)(opcode - 0x38));
          break;
        case 0x3E:
          dataSegmentRegister = SegmentRegister.DS;
          break;
        case 0x3F:
          Debug("AAS");
          Aas();
          break;
        case 0x40:
        case 0x41:
        case 0x42:
        case 0x43:
        case 0x44:
        case 0x45:
        case 0x46:
        case 0x47:
          Debug($"INC {RegisterNames[opcode - 0x40]}");
          IncRegister((byte)(opcode - 0x40));
          break;
        case 0x48:
        case 0x49:
        case 0x4A:
        case 0x4B:
        case 0x4C:
        case 0x4D:
        case 0x4E:
        case 0x4F:
          Debug($"DEC {RegisterNames[opcode - 0x48]}");
          DecRegister((byte)(opcode - 0x48));
          break;
        case 0x50:
        case 0x51:
        case 0x52:
        case 0x53:
        case 0x54:
        case 0x55:
        case 0x56:
        case 0x57:
          Debug($"PUSH {RegisterNames[opcode - 0x50]}");
          Push(Registers[opcode - 0x50]);
          break;
        case 0x58:
        case 0x59:
        case 0x5A:
        case 0x5B:
        case 0x5C:
        case 0x5D:
        case 0x5E:
        case 0x5F:
          Debug($"POP {RegisterNames[opcode - 0x58]}");
          Registers[opcode - 0x58] = Pop();
          break;
        case 0x70:
          JumpShortConditional(Flags.HasFlag(CpuFlags.Overflow), "JO");
          break;
        case 0x71:
          JumpShortConditional(!Flags.HasFlag(CpuFlags.Overflow), "JNO");
          break;
        case 0x72:
          JumpShortConditional(Flags.HasFlag(CpuFlags.Carry), "JC");
          break;
        case 0x73:
          JumpShortConditional(!Flags.HasFlag(CpuFlags.Carry), "JNC");
          break;
        case 0x74:
          JumpShortConditional(Flags.HasFlag(CpuFlags.Zero), "JZ");
          break;
        case 0x75:
          JumpShortConditional(!Flags.HasFlag(CpuFlags.Zero), "JNZ");
          break;
        case 0x76:
          JumpShortConditional(BelowOrEqual, "JBE");
          break;
        case 0x77:
          JumpShortConditional(!BelowOrEqual, "JNBE");
          break;
        case 0x78:
          JumpShortConditional(Flags.HasFlag(CpuFlags.Sign), "JS");
          break;
        case 0x79:
          JumpShortConditional(!Flags.HasFlag(CpuFlags.Sign), "JNS");
          break;
        case 0x7A:
          JumpShortConditional(Flags.HasFlag(CpuFlags.Parity), "JP");
          break;
        case 0x7B:
          JumpShortConditional(!Flags.HasFlag(CpuFlags.Parity), "JNP");
          break;
        case 0x7C:
          JumpShortConditional(Less, "JL");
          break;
        case 0x7D:
          JumpShortConditional(!Less, "JNL");
          break;
        case 0x7E:
          JumpShortConditional(LessOrEqual, "JLE");
          break;
        case 0x7F:
          JumpShortConditional(!LessOrEqual, "JNLE");
          break;
        case 0x80:
        case 0x81:
        case 0x82:
        case 0x83:
          Add_Or_Adc_Sbb_And_Sub_Xor_Cmp(opcode);
          break;
        case 0x84:
        case 0x85:
          Debug("TEST");
          Test(opcode);
          break;
        case 0x86:
        case 0x87:
          Debug("XCHG");
          Xchg(opcode);
          break;
        case 0x88:
        case 0x89:
        case 0x8A:
        case 0x8B:
          Debug("MOV");
          MovRegMem(opcode);
          break;
        case 0x8C:
          Debug("MOV");
          MovRegMemToSegReg();
          break;
        case 0x8D:
          Debug("LEA");
          Lea();
          break;
        case 0x8E:
          Debug("MOV");
          MovSegRegToRegMem();
          break;
        case 0x8F:
          Debug("POP");
          PopRegMem();
          break;
        case 0x90:
        case 0x91:
        case 0x92:
        case 0x93:
        case 0x94:
        case 0x95:
        case 0x96:
        case 0x97:
          Debug(opcode == 0x90 ? "NOP" : $"XCHG AX,{RegisterNames[opcode - 0x90]}");
          XchgAx((byte)(opcode - 0x90));
          break;
        case 0x98:
          Debug("CBW");
          Cbw();
          break;
        case 0x99:
          Debug("CWD");
          Cwd();
          break;
        case 0x9A:
        {
          var offset = ReadCodeWord();
          var segment = ReadCodeWord();
          Debug($"CALL {segment:X4}:{offset:X4}");
          Call(offset, segment);
          break;
        }
        case 0x9B:
          Debug("WAIT");
          Wait();
          break;
        case 0x9C:
          Debug("PUSHF");
          Pushf();
          break;
        case 0x9D:
          Debug("POPF");
          Popf();
          break;
        case 0x9E:
          Debug("SAHF");
          Sahf();
          break;
        case 0x9F:
          Debug("LAHF");
          Lahf();
          break;
        case 0xA0:
        {
          var addr = ReadCodeWord();
          Debug($"MOV AL,[{addr:X4}]");
          AL = memory.ReadByte(addr);
          break;
        }
        case 0xA1:
        {
          var addr = ReadCodeWord();
          Debug($"MOV AX,[{addr:X4}]");
          AX = memory.ReadWord(addr);
          break;
        }
        case 0xA2:
        {
          var addr = ReadCodeWord();
          Debug($"MOV [{addr:X4}],AL");
          memory.WriteByte(addr, AL);
          break;
        }
        case 0xA3:
        {
          var addr = ReadCodeWord();
          Debug($"MOV [{addr:X4}],AX");
          memory.WriteWord(addr, AX);
          break;
        }
        case 0xA4:
        case 0xA5:
        {
          Debug("MOVS");
          var width = GetOpWidth(opcode);
          RepeatCX(() => Movs(width));
          break;
        }
        case 0xA6:
        case 0xA7:
          Debug("CMPS");
          Cmps(GetOpWidth(opcode));
          break;
        case 0xA8:
        {
          var value = ReadCodeByte();
          Debug($"TEST AL,{value:X2}");
          Test(OpWidth.Byte, AL, value);
          break;
        }
        case 0xA9:
        {
          var value = ReadCodeWord();
          Debug($"TEST AX,{value:X4}");
          Test(OpWidth.Word, AX, value);
          break;
        }
        case 0xAA:
        case 0xAB:
          Debug("STOS");
          Stos(GetOpWidth(opcode));
          break;
        case 0xAC:
        case 0xAD:
          Debug("LODS");
          Lods(GetOpWidth(opcode));
          break;
        case 0xAE:
        case 0xAF:
          Debug("SCAS");
          Scas(GetOpWidth(opcode));
          break;
        case 0xB0:
        case 0xB1:
        case 0xB2:
        case 0xB3:
        case 0xB4:
        case 0xB5:
        case 0xB6:
        case 0xB7:
        {
          var value = ReadCodeByte();
          Debug($"MOV {RegisterNames8[opcode - 0xB0]},{value:X2}");
          SetRegister8(opcode - 0xB0, value);
          break;
        }
        case 0xB8:
        case 0xB9:
        case 0xBA:
        case 0xBB:
        case 0xBC:
        case 0xBD:
        case 0xBE:
        case 0xBF:
        {
          var value = ReadCodeWord();
          Debug($"MOV {RegisterNames[opcode - 0xB8]},{value:X4}");
          Registers[opcode - 0xB8] = value;
          break;
        }
        case 0xC2:
          Debug("RET");
          RetIntraSeg(ReadCodeWord());
          break;
        case 0xC3:
          Debug("RET");
          RetIntraSeg();
          break;
        case 0xC4:
          Debug("LES");
          Les();
          break;
        case 0xC5:
          Debug("LDS");
          Lds();
          break;
        case 0xC6:
        case 0xC7:
          Debug("MOV");
          MovMemValue(opcode);
          break;
        case 0xCA:
          Debug("RET");
          RetInterSeg(ReadCodeWord());
          break;
        case 0xCB:
          Debug("RET");
          RetInterSeg();
          break;
        case 0xCC:
          Debug("INT 3");
          Int(InterruptVector.Breakpoint);
          break;
        case 0xCD:
          Debug("INT");
          Int((InterruptVector)ReadCodeByte());
          break;
        case 0xCE:
          Debug("INTO");
          Into();
          break;
        case 0xCF:
          Debug("IRET");
          Iret();
          break;
        case 0xD0:
        case 0xD1:
        case 0xD2:
        case 0xD3:
          Rol_Ror_Rcl_Rcr_Shl_Shr(opcode);
          break;
        case 0xD4:
          Debug("AAM");
          Aam();
          break;
        case 0xD5:
          Debug("AAD");
          Aad();
          break;
        case 0xD7:
          Debug("XLAT");
          Xlat();
          break;
        case 0xD8:
        case 0xD9:
        case 0xDA:
        case 0xDB:
        case 0xDC:
        case 0xDD:
        case 0xDE:
        case 0xDF:
          Debug("ESC");
          Esc();
          break;
        case 0xE0:
          LoopNotEqual();
          break;
        case 0xE1:
          LoopEqual();
          break;
        case 0xE2:
          Loop();
          break;
        case 0xE3:
          JumpShortConditional(CX == 0, "JCXZ");
          break;
        case 0xE4:
          Debug("IN AL,");
          AL = PortIn08(ReadCodeByte());
          break;
        case 0xE5:
          Debug("IN AX,");
          AX = PortIn16(ReadCodeByte());
          break;
        case 0xE6:
          Debug("OUT AL,");
          PortOut(ReadCodeByte(), AL);
          break;
        case 0xE7:
          Debug("OUT AX,");
          PortOut(AX, ReadCodeByte());
          break;
        case 0xE8:
        {
          var relAddr = (short)ReadCodeWord();
          Debug($"CALL {SignedHex(relAddr)}");
          Call((ushort)(IP + relAddr));
          break;
        }
        case 0xE9:
        {
          var relAddr = (short)ReadCodeWord();
          Debug($"JMP {SignedHex(relAddr)}");
          Jmp((ushort)(IP + relAddr), CS);
          break;
        }
        case 0xEA:
        {
          var offset = ReadCodeWord();
          var segment = ReadCodeWord();
          Debug($"JMP {segment:X4}:{offset:X4}");
          Jmp(offset, segment);
          break;
        }
        case 0xEB:
        {
          var relAddr = (sbyte)ReadCodeByte();
          Debug($"JMP {SignedHex(relAddr)}");
          Jmp((ushort)(IP + relAddr), CS);
          break;
        }
        case 0xEC:
          Debug("IN AL,DX");
          AL = PortIn08(DX);
          break;
        case 0xED:
          Debug("IN AX,DX");
          AX = PortIn16(DX);
          break;
        case 0xEE:
          Debug("OUT AL,DX");
          PortOut(DX, AL);
          break;
        case 0xEF:
          Debug("OUT AX,DX");
          PortOut(DX, AX);
          break;
        case 0xF0:
          Debug("LOCK");
          Lock();
          break;
        case 0xF2:
          Debug("REPNE");
          repeat = Repeat.Negative;
          break;
        case 0xF3:
          Debug("REP");
          repeat = Repeat.Positive;
          break;
        case 0xF4:
          Debug("HLT");
          Hlt();
          break;
        case 0xF5:
          Debug("CMC");
          ToggleFlag(CpuFlags.Carry);
          break;
        case 0xF6:
        case 0xF7:
          Test_Not_Neg_Mul_Imul_Div_Idiv(opcode);
          break;
        case 0xF8:
          Debug("CLC");
          ClearFlag(CpuFlags.Carry);
          break;
        case 0xF9:
          Debug("STC");
          SetFlag(CpuFlags.Carry);
          break;
        case 0xFA:
          Debug("CLI");
          ClearFlag(CpuFlags.InterruptEnable);
          break;
        case 0xFB:
          Debug("STI");
          SetFlag(CpuFlags.InterruptEnable);
          break;
        case 0xFC:
          Debug("CLD");
          ClearFlag(CpuFlags.Direction);
          break;
        case 0xFD:
          Debug("STD");
          SetFlag(CpuFlags.Direction);
          break;
        case 0xFE:
          Inc_Dec(opcode);
          break;
        case 0xFF:
          Inc_Dec_Call_Jmp_Push();
          break;
        default:
          UnknownOpcode(opcode);
          break;
      }
      Debug(" " + debugParam + Environment.NewLine);
    }

    public void Reset()
    {
      Flags = CpuFlags.AlwaysOn;
      CS = SpecialOffset.BootStrapping >> 4;
      IP = 0x0000;
      DS = SS = ES = 0x0000;
      Array.Clear(Registers, 0, Registers.Length);
      dataSegmentRegister = SegmentRegister.DS;
      repeat = Repeat.No;
    }


    private static BitArray CalcParityTable()
    {
      var parity = new BitArray(byte.MaxValue + 1);
      for (var i = 0; i < parity.Length; i++)
      {
        byte isEvenBit = 1;
        for (var j = 0; j < 8; j++)
        {
          var mask = 1 << j;
          if ((i & mask) != 0) { isEvenBit ^= 1; }
        }
        if (isEvenBit == 1)
        {
          parity.Set(i, true);
        }
      }
      return parity;
    }


    [Conditional("DEBUG")]
    private void Debug(string text)
    {
      if (text != null)
      {
        System.Diagnostics.Debug.Write(text);
      }
      debugParam = null;
    }

    [Conditional("DEBUG")]
    private void DebugSourceThenTarget(string text)
    {
      if (debugParam == null)
      {
        debugParam = text;
      }
      else
      {
        debugParam = text + "," + debugParam;
      }
    }

    private int GetEffectiveAddress(byte mod, byte rm, bool useSegment = true)
    {
      string addrText = null;
      ushort disp = 0;
      if (mod == 0b01)
      {
        // 8-bit displacement
        disp = ReadCodeByte();
        addrText = "+" + disp.ToString("X2");
      }
      else if (mod == 0b10)
      {
        // 16-bit displacement
        disp = ReadCodeWord();
        addrText = "+" + disp.ToString("X4");
      }

      var ea = 0;
      switch (rm)
      {
        case 0b000:
          DebugSourceThenTarget($"[BX+SI{addrText}]");
          ea = BX + SI + disp;
          break;
        case 0b001:
          DebugSourceThenTarget($"[BX+DI{addrText}]");
          ea = BX + DI + disp;
          break;
        case 0b010:
          DebugSourceThenTarget($"[BP+SI{addrText}]");
          ea = BP + SI + disp;
          break;
        case 0b011:
          DebugSourceThenTarget($"[BP+DI{addrText}]");
          ea = BP + DI + disp;
          break;
        case 0b100:
          DebugSourceThenTarget($"[SI{addrText}]");
          ea = SI + disp;
          break;
        case 0b101:
          DebugSourceThenTarget($"[DI{addrText}]");
          ea = DI + disp;
          break;
        case 0b110:
          if (mod == 0b00)
          {
            ea = ReadCodeWord();
            DebugSourceThenTarget($"[{ea:X4}]");
          }
          else
          {
            DebugSourceThenTarget($"[BP{addrText}]");
            ea = BP + disp;
          }
          break;
        case 0b111:
          DebugSourceThenTarget($"[BX{addrText}]");
          ea = BX + disp;
          break;
      }
      int effectiveAddress = (ushort)ea;
      if (useSegment) { effectiveAddress += DataSegment * 16; }
      currentEffectiveAddress = effectiveAddress;
      return effectiveAddress;
    }

    private static OpWidth GetOpWidth(byte opcode)
    {
      return (opcode & 0x1) == 0 ? OpWidth.Byte : OpWidth.Word;
    }

    private string GetRegisterName(OpWidth width, int register)
    {
      return width == OpWidth.Byte ? RegisterNames8[register] : RegisterNames[register];
    }

    private ushort GetRegisterValue(OpWidth width, int index)
    {
      return width == OpWidth.Byte ? GetRegister8(index) : Registers[index];
    }

    private ushort GetSegmentRegisterValue(SegmentRegister segmentRegister)
    {
      switch (segmentRegister)
      {
        case SegmentRegister.ES:
          return ES;
        case SegmentRegister.CS:
          return CS;
        case SegmentRegister.SS:
          return SS;
        case SegmentRegister.DS:
          return DS;
        default:
          throw new ArgumentOutOfRangeException(nameof(segmentRegister));
      }
    }

    private void HandleLogicalOpGroup(byte relOpcode, Func<byte, byte, byte> func8, Func<ushort, ushort, ushort> func16)
    {
      HandleOpGroup(relOpcode,
        (x, y) => (byte)SetFlagsForLogicalOp(OpWidth.Byte, func8(x, y)),
        (x, y) => SetFlagsForLogicalOp(OpWidth.Word, func16(x, y)));
    }

    private void HandleOpGroup(byte relOpcode, Func<byte, byte, byte?> func8, Func<ushort, ushort, ushort?> func16)
    {
      if (relOpcode == 4) // XXX AL, IMM8
      {
        AL = func8(AL, ReadCodeByte()) ?? AL;
        return;
      }
      if (relOpcode == 5) // XXX AX, IMM16
      {
        AX = func16(AX, ReadCodeWord()) ?? AX;
        return;
      }

      var (mod, reg, rm) = ReadModRegRm();

      var width = GetOpWidth(relOpcode);

      ushort src, dst;
      if (IsRegisterSource(relOpcode))
      {
        dst = ReadFromRegisterOrMemory(width, mod, rm);
        src = GetRegisterValue(width, reg);
        DebugSourceThenTarget(GetRegisterName(width, reg));
      }
      else
      {
        dst = GetRegisterValue(width, reg);
        DebugSourceThenTarget(GetRegisterName(width, reg));
        src = ReadFromRegisterOrMemory(width, mod, rm);
      }

      var result = width == OpWidth.Byte ? func8((byte)dst, (byte)src) : func16(dst, src);
      if (result == null) { return; }

      if (IsRegisterSource(relOpcode))
      {
        WriteToRegisterOrMemory(width, mod, rm, () => result.Value);
      }
      else
      {
        SetRegisterValue(width, reg, result.Value);
      }
    }

    private static bool IsRegisterSource(byte opcode)
    {
      return ((opcode >> 1) & 0x1) == 0;
    }

    private byte ReadCodeByte()
    {
      return memory.ReadByte(CS * 16 + IP++);
    }

    private ushort ReadCodeWord()
    {
      var value = memory.ReadWord(CS * 16 + IP);
      IP += 2;
      return value;
    }

    private ushort ReadFromMemory(OpWidth width, int effectiveAddress)
    {
      return width == OpWidth.Byte ? memory.ReadByte(effectiveAddress) : memory.ReadWord(effectiveAddress);
    }

    private ushort ReadFromRegisterOrMemory(OpWidth width, byte mod, byte rm)
    {
      if (mod == 0b11)
      {
        DebugSourceThenTarget(GetRegisterName(width, rm));
        return GetRegisterValue(width, rm);
      }
      var addr = currentEffectiveAddress ?? GetEffectiveAddress(mod, rm);
      return ReadFromMemory(width, addr);
    }

    private (byte mod, byte reg, byte rm) ReadModRegRm()
    {
      var b = ReadCodeByte();
      return ((byte)(b >> 6), (byte)((b >> 3) & 0b111), (byte)(b & 0b111));
    }


    private void SetFlag(CpuFlags flag)
    {
      Flags |= flag;
    }

    private void SetFlag(CpuFlags flag, bool set)
    {
      if (set) { SetFlag(flag); }
      else { ClearFlag(flag); }
    }

    private ushort SetFlagsForLogicalOp(OpWidth width, ushort result)
    {
      ClearFlag(CpuFlags.Overflow | CpuFlags.Carry);
      SetFlagsFromValue(width, result);
      return result;
    }

    private void SetFlagsFromValue(OpWidth width, ushort word)
    {
      var msb = width == OpWidth.Word ? (ushort)0x8000 : (ushort)0x80;
      SetFlag(CpuFlags.Parity, parity.Get((byte)word));
      SetFlag(CpuFlags.Zero, word == 0);
      SetFlag(CpuFlags.Sign, (word & msb) != 0);
    }

    private void SetRegisterValue(OpWidth width, int index, ushort value)
    {
      if (width == OpWidth.Word)
      {
        Registers[index] = value;
      }
      else
      {
        SetRegister8(index, (byte)value);
      }
    }

    private void SetSegmentRegisterValue(SegmentRegister segmentRegister, ushort value)
    {
      switch (segmentRegister)
      {
        case SegmentRegister.ES:
          ES = value;
          break;
        case SegmentRegister.CS:
          CS = value;
          break;
        case SegmentRegister.SS:
          SS = value;
          break;
        case SegmentRegister.DS:
          DS = value;
          break;
        default:
          throw new ArgumentOutOfRangeException(nameof(segmentRegister));
      }
    }

    private static string SignedHex(sbyte value)
    {
      return $"{(value < 0 ? '-' : '+')}{Math.Abs(value):X2}";
    }

    private static string SignedHex(short value)
    {
      return $"{(value < 0 ? '-' : '+')}{Math.Abs(value):X4}";
    }


    private void ToggleFlag(CpuFlags flag)
    {
      Flags ^= flag;
    }

    private void UnknownOpcode(byte opcode)
    {
      Debug($"Opcode {opcode:X2} not supported");
      Int(InterruptVector.InvalidOpcode);
    }

    private void UnknownOpcode(byte opcode, byte mod, byte reg, byte rm)
    {
      var modRegRm = (mod << 6) | (reg << 3) | rm;
      Debug($"Opcode {opcode:X2}{modRegRm:X2} not supported");
      Int(InterruptVector.InvalidOpcode);
    }


    private void WriteToMemory(OpWidth width, int effectiveAddress, ushort value)
    {
      if (width == OpWidth.Byte)
      {
        memory.WriteByte(effectiveAddress, (byte)value);
      }
      else
      {
        memory.WriteWord(effectiveAddress, value);
      }
    }

    private void WriteToRegisterOrMemory(OpWidth width, byte mod, byte rm, Func<ushort> value)
    {
      if (mod == 0b11)
      {
        DebugSourceThenTarget(GetRegisterName(width, rm));
        SetRegisterValue(width, rm, value());
      }
      else
      {
        var addr = currentEffectiveAddress ?? GetEffectiveAddress(mod, rm);
        WriteToMemory(width, addr, value());
      }
    }

    private enum OpWidth
    {
      Byte = 1,
      Word = 2
    }

    private enum Repeat
    {
      No,
      Positive,
      Negative
    }

    [SuppressMessage("ReSharper", "InconsistentNaming")]
    private enum SegmentRegister : byte
    {
      ES,
      CS,
      SS,
      DS
    }
  }
}