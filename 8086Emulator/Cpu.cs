using System;
using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Masch._8086Emulator.Ports;

namespace Masch._8086Emulator
{
  public partial class Cpu : CpuState
  {
    public const int Frequency = ProgrammableInterruptTimer8253.Frequency * 4; // 4.77 MHz
    public const int CpuTicksPerTimerTicks = Frequency / ProgrammableInterruptTimer8253.Frequency;

    private readonly Machine machine;
    private readonly MemoryController memory;
    private readonly byte[] opcodes = new byte[6];
    private readonly BitArray parity;
    private int clockCount;
    private int cpuTickOfLastPitTick;
    private int? currentEffectiveAddress;
    private SegmentRegister dataSegmentRegister;
    private bool dataSegmentRegisterChanged;
    private int loopingCount;
    private string debugParam;
    private byte opcodeIndex;
    private Action[] operations;
    private Repeat repeat;

    public Cpu(Machine machine)
    {
      this.machine = machine;
      memory = machine.MemoryController;

      RegisterOperations();
      parity = CalcParityTable();
      Reset();
    }

    // ReSharper disable once ConvertToAutoPropertyWithPrivateSetter
    public int ClockCount => clockCount;

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

    private Width OpWidth => (opcodes[0] & 0x1) == 0 ? Width.Byte : Width.Word;

    private bool IsRegisterSource => ((opcodes[0] >> 1) & 0x1) == 0;

    public void Reset()
    {
      CarryFlag = ParityFlag = AuxiliaryCarryFlag = ZeroFlag = SignFlag = TrapFlag = InterruptEnableFlag = DirectionFlag = OverflowFlag = false;
      CS = SpecialOffset.BootStrapping >> 4;
      IP = 0x0000;
      DS = SS = ES = 0x0000;
      Array.Clear(Registers, 0, Registers.Length);
      dataSegmentRegister = SegmentRegister.DS;
      dataSegmentRegisterChanged = false;
      repeat = Repeat.No;
      clockCount = 0;
      cpuTickOfLastPitTick = 0;
      loopingCount = 0;
    }

    public void Tick()
    {
      var remainingPitTicks = (clockCount - cpuTickOfLastPitTick) / CpuTicksPerTimerTicks;
      if (remainingPitTicks > 0)
      {
        for (int i = cpuTickOfLastPitTick; i < clockCount; i += CpuTicksPerTimerTicks)
        {
          machine.Pit.Tick();
          cpuTickOfLastPitTick = i;
        }
      }

      Debug($"[{CS:X4}:{IP:X4}] ");

      currentEffectiveAddress = null;
      Array.Copy(memory.Memory, (CS << 4) + IP, opcodes, 0, 6);
      opcodeIndex = 0;
      var opcode = ReadCodeByte();
      operations[opcode]();

      if (!dataSegmentRegisterChanged) { dataSegmentRegister = SegmentRegister.DS; }
      else { dataSegmentRegisterChanged = false; }

      Debug(" " + debugParam + Environment.NewLine);
    }

    protected void RegisterOperations()
    {
      operations = new Action[]
      {
        Add, // 0x00
        Add, // 0x01
        Add, // 0x02
        Add, // 0x03
        Add, // 0x04
        Add, // 0x05
        PushSegment, // 0x06
        PopSegment, // 0x07
        Or, // 0x08
        Or, // 0x09
        Or, // 0x0A
        Or, // 0x0B
        Or, // 0x0C
        Or, // 0x0D
        PushSegment, // 0x0E
        UnknownOpcode, // 0x0F
        Adc, // 0x10
        Adc, // 0x11
        Adc, // 0x12
        Adc, // 0x13
        Adc, // 0x14
        Adc, // 0x15
        PushSegment, // 0x16
        PopSegment, // 0x17
        Sbb, // 0x18
        Sbb, // 0x19
        Sbb, // 0x1A
        Sbb, // 0x1B
        Sbb, // 0x1C
        Sbb, // 0x1D
        PushSegment, // 0x1E
        PopSegment, // 0x1F
        And, // 0x20
        And, // 0x21
        And, // 0x22
        And, // 0x23
        And, // 0x24
        And, // 0x25
        ChangeSegmentPrefix, // 0x26
        Daa, // 0x27
        Sub, // 0x28
        Sub, // 0x29
        Sub, // 0x2A
        Sub, // 0x2B
        Sub, // 0x2C
        Sub, // 0x2D
        ChangeSegmentPrefix, // 0x2E
        Das, // 0x2F
        Xor, // 0x30
        Xor, // 0x31
        Xor, // 0x32
        Xor, // 0x33
        Xor, // 0x34
        Xor, // 0x35
        ChangeSegmentPrefix, // 0x36
        Aaa, // 0x37 
        Cmp, // 0x38
        Cmp, // 0x39
        Cmp, // 0x3A
        Cmp, // 0x3B
        Cmp, // 0x3C
        Cmp, // 0x3D
        ChangeSegmentPrefix, // 0x3E
        Aas, // 0x3F
        IncRegister, // 0x40
        IncRegister, // 0x41
        IncRegister, // 0x42
        IncRegister, // 0x43
        IncRegister, // 0x44
        IncRegister, // 0x45
        IncRegister, // 0x46
        IncRegister, // 0x47
        DecRegister, // 0x48
        DecRegister, // 0x49
        DecRegister, // 0x4A
        DecRegister, // 0x4B
        DecRegister, // 0x4C
        DecRegister, // 0x4D
        DecRegister, // 0x4E
        DecRegister, // 0x4F
        PushRegister, // 0x50
        PushRegister, // 0x51
        PushRegister, // 0x52
        PushRegister, // 0x53
        PushRegister, // 0x54
        PushRegister, // 0x55
        PushRegister, // 0x56
        PushRegister, // 0x57
        PopRegister, // 0x58
        PopRegister, // 0x59
        PopRegister, // 0x5A
        PopRegister, // 0x5B
        PopRegister, // 0x5C
        PopRegister, // 0x5D
        PopRegister, // 0x5E
        PopRegister, // 0x5F
        UnknownOpcode, // 0x60
        UnknownOpcode, // 0x61
        UnknownOpcode, // 0x62
        UnknownOpcode, // 0x63
        UnknownOpcode, // 0x64
        UnknownOpcode, // 0x65
        UnknownOpcode, // 0x66
        UnknownOpcode, // 0x67
        UnknownOpcode, // 0x68
        UnknownOpcode, // 0x69
        UnknownOpcode, // 0x6A
        UnknownOpcode, // 0x6B
        UnknownOpcode, // 0x6C
        UnknownOpcode, // 0x6D
        UnknownOpcode, // 0x6E
        UnknownOpcode, // 0x6F
        () => JumpShortConditional(OverflowFlag, "JO"), // 0x70
        () => JumpShortConditional(!OverflowFlag, "JNO"), // 0x71
        () => JumpShortConditional(CarryFlag, "JC"), // 0x72
        () => JumpShortConditional(!CarryFlag, "JNC"), // 0x73
        () => JumpShortConditional(ZeroFlag, "JZ"), // 0x74
        () => JumpShortConditional(!ZeroFlag, "JNZ"), // 0x75
        () => JumpShortConditional(BelowOrEqual, "JBE"), // 0x76
        () => JumpShortConditional(!BelowOrEqual, "JNBE"), // 0x77
        () => JumpShortConditional(SignFlag, "JS"), // 0x78
        () => JumpShortConditional(!SignFlag, "JNS"), // 0x79
        () => JumpShortConditional(ParityFlag, "JP"), // 0x7A
        () => JumpShortConditional(!ParityFlag, "JNP"), // 0x7B
        () => JumpShortConditional(Less, "JL"), // 0x7C
        () => JumpShortConditional(!Less, "JNL"), // 0x7D
        () => JumpShortConditional(LessOrEqual, "JLE"), // 0x7E
        () => JumpShortConditional(!LessOrEqual, "JNLE"), // 0x7F
        OpcodeGroup1, // 0x80
        OpcodeGroup1, // 0x81
        OpcodeGroup1, // 0x82
        OpcodeGroup1, // 0x83
        Test, // 0x84
        Test, // 0x85
        Xchg, // 0x86
        Xchg, // 0x87
        MovRegMem, // 0x88
        MovRegMem, // 0x89
        MovRegMem, // 0x8A
        MovRegMem, // 0x8B
        MovSegRegToRegMem, // 0x8C
        Lea, // 0x8D
        MovRegMemToSegReg, // 0x8E
        PopRegMem, // 0x8F
        Nop, // 0x90
        XchgAx, // 0x91
        XchgAx, // 0x92
        XchgAx, // 0x93
        XchgAx, // 0x94
        XchgAx, // 0x95
        XchgAx, // 0x96
        XchgAx, // 0x97
        Cbw, // 0x98
        Cwd, // 0x99
        () => // 0x9A
        {
          var offset = ReadCodeWord();
          var segment = ReadCodeWord();
          Debug($"CALL {segment:X4}:{offset:X4}");
          DoCall(offset, segment);

          clockCount += 28;
        },
        Wait, // 0x9B
        PushFlags, // 0x9C        
        PopFlags, // 0x9D
        Sahf, // 0x9E
        Lahf, // 0x9F
        () => // 0xA0
        {
          var addr = ReadCodeWord();
          Debug($"MOV AL,[{addr:X4}]");
          AL = memory.ReadByte(addr);

          clockCount += 10;
        },
        () => // 0xA1
        {
          var addr = ReadCodeWord();
          Debug($"MOV AX,[{addr:X4}]");
          AX = memory.ReadWord(addr);

          clockCount += 10;
        },
        () => // 0xA2
        {
          var addr = ReadCodeWord();
          Debug($"MOV [{addr:X4}],AL");
          memory.WriteByte(addr, AL);

          clockCount += 10;
        },
        () => // 0xA3
        {
          var addr = ReadCodeWord();
          Debug($"MOV [{addr:X4}],AX");
          memory.WriteWord(addr, AX);

          clockCount += 10;
        },
        () => RepeatCX(Movs), // 0xA4
        () => RepeatCX(Movs), // 0xA5
        Cmps, // 0xA6
        Cmps, // 0xA7
        () => Test(AL, ReadCodeByte()), // 0xA8
        () => Test(AX, ReadCodeWord()), // 0xA9
        Stos, // 0xAA
        Stos, // 0xAB
        Lods, // 0xAC
        Lods, // 0xAD
        Scas, // 0xAE
        Scas, // 0xAF
        MoveRegisterImmediate08, // 0xB0
        MoveRegisterImmediate08, // 0xB1
        MoveRegisterImmediate08, // 0xB2
        MoveRegisterImmediate08, // 0xB3
        MoveRegisterImmediate08, // 0xB4
        MoveRegisterImmediate08, // 0xB5
        MoveRegisterImmediate08, // 0xB6
        MoveRegisterImmediate08, // 0xB7
        MoveRegisterImmediate16, // 0xB8
        MoveRegisterImmediate16, // 0xB9
        MoveRegisterImmediate16, // 0xBA
        MoveRegisterImmediate16, // 0xBB
        MoveRegisterImmediate16, // 0xBC
        MoveRegisterImmediate16, // 0xBD
        MoveRegisterImmediate16, // 0xBE
        MoveRegisterImmediate16, // 0xBF
        UnknownOpcode, // 0xC0
        UnknownOpcode, // 0xC1
        () => RetIntraSeg(ReadCodeWord()), // 0xC2
        () => RetIntraSeg(), // 0xC3
        Les, // 0xC4
        Lds, // 0xC5
        MovMemValue, // 0xC6
        MovMemValue, // 0xC7
        UnknownOpcode, // 0xC8
        UnknownOpcode, // 0xC9
        () => RetInterSeg(ReadCodeWord()), // 0xCA
        () => RetInterSeg(), // 0xCB
        () => // 0xCC
        {
          Debug("INT 3");
          Int(InterruptVector.Breakpoint);
        },
        () => // 0xCD
        {
          Debug("INT");
          Int((InterruptVector)ReadCodeByte());
        },
        Into, // 0xCE
        Iret, // 0xCF
        OpcodeGroup2, // 0xD0
        OpcodeGroup2, // 0xD1
        OpcodeGroup2, // 0xD2
        OpcodeGroup2, // 0xD3
        Aam, // 0xD4
        Aad, // 0xD5
        UnknownOpcode, // 0xD6
        Xlat, // 0xD7
        Esc, // 0xD8
        Esc, // 0xD9
        Esc, // 0xDA
        Esc, // 0xDB
        Esc, // 0xDC
        Esc, // 0xDD
        Esc, // 0xDE
        Esc, // 0xDF
        LoopNotEqual, // 0xE0
        LoopEqual, // 0xE1
        Loop, // 0xE2
        () => JumpShortConditional(CX == 0, "JCXZ"), // 0xE3
        () => // 0xE4
        {
          AL = PortIn08(ReadCodeByte());
          clockCount += 10;
        },
        () => // 0xE5
        {
          AX = PortIn16(ReadCodeByte());
          clockCount += 10;
        },
        () => // 0xE6
        {
          PortOut08(ReadCodeByte());
          clockCount += 10;
        },
        () => // 0xE7
        {
          PortOut16(ReadCodeByte());
          clockCount += 10;
        },
        () => // 0xE8
        {
          var relAddr = (short)ReadCodeWord();
          Debug($"CALL {SignedHex(relAddr)}");
          DoCall((ushort)(IP + relAddr));

          clockCount += 19;
        },
        () => // 0xE9
        {
          var relAddr = (short)ReadCodeWord();
          Debug($"JMP {SignedHex(relAddr)}");
          DoJmp((ushort)(IP + relAddr), CS);

          clockCount += 15;
        },
        () => // 0xEA
        {
          var offset = ReadCodeWord();
          var segment = ReadCodeWord();
          Debug($"JMP {segment:X4}:{offset:X4}");
          DoJmp(offset, segment);

          clockCount += 15;
        },
        () => // 0xEB
        {
          var relAddr = (sbyte)ReadCodeByte();
          Debug($"JMP {SignedHex(relAddr)}");
          DoJmp((ushort)(IP + relAddr), CS);

          clockCount += 15;
        },
        () => // 0xEC
        {
          Debug("IN AL,DX");
          AL = PortIn08(DX, false);

          clockCount += 8;
        },
        () => // 0xED
        {
          Debug("IN AX,DX");
          AX = PortIn16(DX, false);

          clockCount += 8;
        },
        () => // 0xEE
        {
          Debug("OUT AL,DX");
          PortOut08(DX, false);

          clockCount += 8;
        },
        () => // 0xEF
        {
          Debug("OUT AX,DX");
          PortOut16(DX, false);

          clockCount += 8;
        },
        Lock, // 0xF0
        UnknownOpcode, // 0xF1
        () => // 0xF2
        {
          Debug("REPNE");
          repeat = Repeat.Negative;

          clockCount += 2 + 9; // 2 per se, +9 when used with an opcode supporting repeat
        },
        () => // 0xF3
        {
          Debug("REP");
          repeat = Repeat.Positive;

          clockCount += 2 + 9; // 2 per se, +9 when used with an opcode supporting repeat
        },
        Hlt, // 0xF4
        () => // 0xF5
        {
          Debug("CMC");
          CarryFlag = !CarryFlag;
          clockCount += 2;
        },
        OpcodeGroup3, // 0xF6
        OpcodeGroup3, // 0xF7
        () => // 0xF8
        {
          Debug("CLC");
          CarryFlag = false;
          clockCount += 2;
        },
        () => // 0xF9
        {
          Debug("STC");
          CarryFlag = true;
          clockCount += 2;
        },
        () => // 0xFA
        {
          Debug("CLI");
          InterruptEnableFlag = false;
          clockCount += 2;
        },
        () => // 0xFB
        {
          Debug("STI");
          InterruptEnableFlag = true;
          clockCount += 2;
        },
        () => // 0xFC
        {
          Debug("CLD");
          DirectionFlag = false;
          clockCount += 2;
        },
        () => // 0xFD
        {
          Debug("STD");
          DirectionFlag = true;
          clockCount += 2;
        },
        OpcodeGroup4, // 0xFE
        OpcodeGroup5 // 0xFF
      };
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
      if (loopingCount > 10)
      {
        debugParam = null;
        return;
      }

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
        clockCount += 4;
      }
      else if (mod == 0b10)
      {
        // 16-bit displacement
        disp = ReadCodeWord();
        addrText = "+" + disp.ToString("X4");
        clockCount += 4;
      }

      var ea = 0;
      switch (rm)
      {
        case 0b000:
          DebugSourceThenTarget($"[BX+SI{addrText}]");
          ea = BX + SI + disp;
          clockCount += 7;
          break;
        case 0b001:
          DebugSourceThenTarget($"[BX+DI{addrText}]");
          ea = BX + DI + disp;
          clockCount += 8;
          break;
        case 0b010:
          DebugSourceThenTarget($"[BP+SI{addrText}]");
          ea = BP + SI + disp;
          clockCount += 8;
          break;
        case 0b011:
          DebugSourceThenTarget($"[BP+DI{addrText}]");
          ea = BP + DI + disp;
          clockCount += 7;
          break;
        case 0b100:
          DebugSourceThenTarget($"[SI{addrText}]");
          ea = SI + disp;
          clockCount += 5;
          break;
        case 0b101:
          DebugSourceThenTarget($"[DI{addrText}]");
          ea = DI + disp;
          clockCount += 5;
          break;
        case 0b110:
          if (mod == 0b00)
          {
            ea = ReadCodeWord();
            DebugSourceThenTarget($"[{ea:X4}]");
            clockCount += 2;
          }
          else
          {
            DebugSourceThenTarget($"[BP{addrText}]");
            ea = BP + disp;
            clockCount += 5;
          }
          break;
        case 0b111:
          DebugSourceThenTarget($"[BX{addrText}]");
          ea = BX + disp;
          clockCount += 5;
          break;
      }
      int effectiveAddress = (ushort)ea;
      if (useSegment) { effectiveAddress += DataSegment << 4; }
      currentEffectiveAddress = effectiveAddress;
      return effectiveAddress;
    }

    private string GetRegisterName(Width width, int register)
    {
      return width == Width.Byte ? RegisterNames8[register] : RegisterNames[register];
    }

    private ushort GetRegisterValue(Width width, int index)
    {
      return width == Width.Byte ? GetRegister8(index) : Registers[index];
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

    private void HandleLogicalOpGroup(Func<byte, byte, byte> func8, Func<ushort, ushort, ushort> func16)
    {
      HandleOpGroup(
        (x, y) => (byte)SetFlagsForLogicalOp(Width.Byte, func8(x, y)),
        (x, y) => SetFlagsForLogicalOp(Width.Word, func16(x, y)));
    }

    private void HandleOpGroup(Func<byte, byte, byte?> func8, Func<ushort, ushort, ushort?> func16)
    {
      var relOpcode = opcodes[0] & 0b111;
      if (relOpcode == 4) // XXX AL, IMM8
      {
        AL = func8(AL, ReadCodeByte()) ?? AL;
        clockCount += 4;
        return;
      }
      if (relOpcode == 5) // XXX AX, IMM16
      {
        AX = func16(AX, ReadCodeWord()) ?? AX;
        clockCount += 4;
        return;
      }

      var (mod, reg, rm) = ReadModRegRm();
      var width = OpWidth;

      ushort src, dst;
      if (IsRegisterSource)
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

      var result = width == Width.Byte ? func8((byte)dst, (byte)src) : func16(dst, src);
      if (result == null) { return; }

      if (IsRegisterSource)
      {
        WriteToRegisterOrMemory(width, mod, rm, () => result.Value);
        clockCount += 16;
      }
      else
      {
        SetRegisterValue(width, reg, result.Value);
        clockCount += 9;
      }
    }

    private byte ReadCodeByte()
    {
      IP++;
      return opcodes[opcodeIndex++];
    }

    private ushort ReadCodeWord()
    {
      IP += 2;
      return (ushort)(opcodes[opcodeIndex++] | (opcodes[opcodeIndex++] << 8));
    }

    private ushort ReadFromMemory(Width width, int effectiveAddress)
    {
      return width == Width.Byte ? memory.ReadByte(effectiveAddress) : memory.ReadWord(effectiveAddress);
    }

    private ushort ReadFromRegisterOrMemory(Width width, byte mod, byte rm)
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

    private ushort SetFlagsForLogicalOp(Width width, ushort result)
    {
      OverflowFlag = CarryFlag = false;
      SetFlagsFromValue(width, result);
      return result;
    }

    private void SetFlagsFromValue(Width width, ushort word)
    {
      var msb = width == Width.Word ? (ushort)0x8000 : (ushort)0x80;
      ParityFlag = parity.Get((byte)word);
      ZeroFlag = word == 0;
      SignFlag = (word & msb) != 0;
    }

    private void SetRegisterValue(Width width, int index, ushort value)
    {
      if (width == Width.Word)
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

    private void UnknownOpcode()
    {
      Debug($"Opcode {opcodes[0]:X2} not supported");
      Int(InterruptVector.InvalidOpcode);
    }

    private void UnknownOpcode(byte mod, byte reg, byte rm)
    {
      var modRegRm = (mod << 6) | (reg << 3) | rm;
      Debug($"Opcode {opcodes[0]:X2}{modRegRm:X2} not supported");
      Int(InterruptVector.InvalidOpcode);
    }


    private void WriteToMemory(Width width, int effectiveAddress, ushort value)
    {
      if (width == Width.Byte)
      {
        memory.WriteByte(effectiveAddress, (byte)value);
      }
      else
      {
        memory.WriteWord(effectiveAddress, value);
      }
    }

    private void WriteToRegisterOrMemory(Width width, byte mod, byte rm, Func<ushort> value)
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

    private enum Width
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