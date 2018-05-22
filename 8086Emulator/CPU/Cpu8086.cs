using System;

namespace Masch._8086Emulator.CPU
{
  public partial class Cpu8086 : Cpu
  {
    public Cpu8086(Machine machine)
      : base(machine)
    {
    }

    protected override Action[] RegisterOperations()
    {
      return new Action[]
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
        PopSegment, // 0x0F, undocumented
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
        () => JumpShortConditional(OverflowFlag, "JO"), // 0x60, undocumented
        () => JumpShortConditional(!OverflowFlag, "JNO"), // 0x61, undocumented
        () => JumpShortConditional(CarryFlag, "JC"), // 0x62, undocumented
        () => JumpShortConditional(!CarryFlag, "JNC"), // 0x63, undocumented
        () => JumpShortConditional(ZeroFlag, "JZ"), // 0x64, undocumented
        () => JumpShortConditional(!ZeroFlag, "JNZ"), // 0x65, undocumented
        () => JumpShortConditional(BelowOrEqual, "JBE"), // 0x66, undocumented
        () => JumpShortConditional(!BelowOrEqual, "JNBE"), // 0x67, undocumented
        () => JumpShortConditional(SignFlag, "JS"), // 0x68, undocumented
        () => JumpShortConditional(!SignFlag, "JNS"), // 0x69, undocumented
        () => JumpShortConditional(ParityFlag, "JP"), // 0x6A, undocumented
        () => JumpShortConditional(!ParityFlag, "JNP"), // 0x6B, undocumented
        () => JumpShortConditional(Less, "JL"), // 0x6C, undocumented
        () => JumpShortConditional(!Less, "JNL"), // 0x6D, undocumented
        () => JumpShortConditional(LessOrEqual, "JLE"), // 0x6E, undocumented
        () => JumpShortConditional(!LessOrEqual, "JNLE"), // 0x6F, undocumented
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
          SetDebug("CALL", $"{segment:X4}:{offset:X4}");
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
          SetDebug("MOV", "AL", $"[{addr:X4}]");
          AL = ReadDataByte(addr);

          clockCount += 10;
        },
        () => // 0xA1
        {
          var addr = ReadCodeWord();
          SetDebug("MOV", "AX", $"[{addr:X4}]");
          AX = ReadDataWord(addr);

          clockCount += 10;
        },
        () => // 0xA2
        {
          var addr = ReadCodeWord();
          SetDebug("MOV", $"[{addr:X4}]", "AL");
          WriteDataByte(addr, AL);

          clockCount += 10;
        },
        () => // 0xA3
        {
          var addr = ReadCodeWord();
          SetDebug("MOV", $"[{addr:X4}]", "AX");
          WriteDataWord(addr, AX);

          clockCount += 10;
        },
        () => RepeatCX(Movs), // 0xA4
        () => RepeatCX(Movs), // 0xA5
        () => RepeatCX(Cmps), // 0xA6
        () => RepeatCX(Cmps), // 0xA7
        () => Test(AL, ReadCodeByte()), // 0xA8
        () => Test(AX, ReadCodeWord()), // 0xA9
        () => RepeatCX(Stos), // 0xAA
        () => RepeatCX(Stos), // 0xAB
        () => RepeatCX(Lods), // 0xAC
        () => RepeatCX(Lods), // 0xAD
        () => RepeatCX(Scas), // 0xAE
        () => RepeatCX(Scas), // 0xAF
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
        () => RetIntraSeg(ReadCodeWord()), // 0xC0, undocumented
        () => RetIntraSeg(), // 0xC1, undocumented
        () => RetIntraSeg(ReadCodeWord()), // 0xC2
        () => RetIntraSeg(), // 0xC3
        Les, // 0xC4
        Lds, // 0xC5
        MovMemValue, // 0xC6
        MovMemValue, // 0xC7
        () => RetInterSeg(ReadCodeWord()), // 0xC8, undocumented
        () => RetInterSeg(), // 0xC9, undocumented
        () => RetInterSeg(ReadCodeWord()), // 0xCA
        () => RetInterSeg(), // 0xCB
        () => // 0xCC
        {
          SetDebug("INT", "3");
          DoInt(InterruptVector.CpuBreakpoint);
        },
        () => // 0xCD
        {
          var vector = ReadCodeByte();
          SetDebug("INT", vector.ToString("X2"));
          DoInt((InterruptVector)vector);
        },
        Into, // 0xCE
        Iret, // 0xCF
        OpcodeGroup2, // 0xD0
        OpcodeGroup2, // 0xD1
        OpcodeGroup2, // 0xD2
        OpcodeGroup2, // 0xD3
        Aam, // 0xD4
        Aad, // 0xD5
        () => AL = CarryFlag ? (byte)0xFF : (byte)0x00, // 0xD6, undocumented
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
          PortOut08(ReadCodeByte(), AL);
          clockCount += 10;
        },
        () => // 0xE7
        {
          PortOut16(ReadCodeByte(), AX);
          clockCount += 10;
        },
        () => // 0xE8
        {
          var relAddr = (short)ReadCodeWord();
          SetDebug("CALL", SignedHex(relAddr));
          DoCall((ushort)(IP + relAddr));

          clockCount += 19;
        },
        () => // 0xE9
        {
          var relAddr = (short)ReadCodeWord();
          SetDebug("JMP", SignedHex(relAddr));
          DoJmp((ushort)(IP + relAddr), CS);

          clockCount += 15;
        },
        () => // 0xEA
        {
          var offset = ReadCodeWord();
          var segment = ReadCodeWord();
          SetDebug("JMP", $"{segment:X4}:{offset:X4}");
          DoJmp(offset, segment);

          clockCount += 15;
        },
        () => // 0xEB
        {
          var relAddr = (sbyte)ReadCodeByte();
          SetDebug("JMP", SignedHex(relAddr));
          DoJmp((ushort)(IP + relAddr), CS);

          clockCount += 15;
        },
        () => // 0xEC
        {
          SetDebug("IN", "AL", "DX");
          AL = PortIn08(DX, false);

          clockCount += 8;
        },
        () => // 0xED
        {
          SetDebug("IN", "AX", "DX");
          AX = PortIn16(DX, false);

          clockCount += 8;
        },
        () => // 0xEE
        {
          SetDebug("OUT", "AL", "DX");
          PortOut08(DX, AL, false);

          clockCount += 8;
        },
        () => // 0xEF
        {
          SetDebug("OUT", "AX", "DX");
          PortOut16(DX, AX, false);

          clockCount += 8;
        },
        Lock, // 0xF0
        () => {}, // 0xF1, undocumented (probably maps to Lock)
        () => // 0xF2
        {
          SetDebug("REPNE");
          repeatWhileNotZero = true;

          clockCount += 2 + 9; // 2 per se, +9 when used with an opcode supporting repeat
        },
        () => // 0xF3
        {
          SetDebug("REP");
          repeatWhileNotZero = false;

          clockCount += 2 + 9; // 2 per se, +9 when used with an opcode supporting repeat
        },
        Hlt, // 0xF4
        () => // 0xF5
        {
          SetDebug("CMC");
          CarryFlag = !CarryFlag;
          clockCount += 2;
        },
        OpcodeGroup3, // 0xF6
        OpcodeGroup3, // 0xF7
        () => // 0xF8
        {
          SetDebug("CLC");
          CarryFlag = false;
          clockCount += 2;
        },
        () => // 0xF9
        {
          SetDebug("STC");
          CarryFlag = true;
          clockCount += 2;
        },
        () => // 0xFA
        {
          SetDebug("CLI");
          InterruptEnableFlag = false;
          clockCount += 2;
        },
        () => // 0xFB
        {
          SetDebug("STI");
          InterruptEnableFlag = true;
          clockCount += 2;
        },
        () => // 0xFC
        {
          SetDebug("CLD");
          DirectionFlag = false;
          clockCount += 2;
        },
        () => // 0xFD
        {
          SetDebug("STD");
          DirectionFlag = true;
          clockCount += 2;
        },
        OpcodeGroup4, // 0xFE
        OpcodeGroup5 // 0xFF
      };
    }
  }
}