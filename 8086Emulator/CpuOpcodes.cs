using System;
using Masch._8086Emulator.InternalDevices;

namespace Masch._8086Emulator
{
  public partial class Cpu
  {
    private void Aaa()
    {
      Debug("AAA");
      if ((AL & 0x0F) > 9 || AuxiliaryCarryFlag)
      {
        AL += 6;
        AH++;
        AuxiliaryCarryFlag = CarryFlag = true;
      }
      else
      {
        AuxiliaryCarryFlag = CarryFlag = false;
      }
      AL &= 0x0F;

      clockCount += 4;
    }

    private void Aad()
    {
      Debug("AAD");
      AX = (byte)(AH * ReadCodeByte() + AL);
      SetFlagsFromValue(Width.Byte, AL);

      clockCount += 60;
    }

    private void Aam()
    {
      Debug("AAM");
      var value = ReadCodeByte();
      if (value == 0)
      {
        Int(InterruptVector.DivideByZero);
      }
      else
      {
        AH = (byte)(AL / value);
        AL = (byte)(AL % value);
        SetFlagsFromValue(Width.Word, AX);
      }

      clockCount += 83;
    }

    private void Aas()
    {
      Debug("AAS");
      if ((AL & 0x0F) > 9 || AuxiliaryCarryFlag)
      {
        AL -= 6;
        AH--;
        AuxiliaryCarryFlag = CarryFlag = true;
      }
      else
      {
        AuxiliaryCarryFlag = CarryFlag = false;
      }
      AL &= 0x0F;

      clockCount += 4;
    }

    private void Adc()
    {
      Debug("ADC");
      var c = CarryFlag ? (byte)1 : (byte)0;
      HandleOpGroup((x, y) => Add08(x, y, c), (x, y) => Add16(x, y, c));
    }

    private void Add()
    {
      Debug("ADD");
      HandleOpGroup((x, y) => Add08(x, y), (x, y) => Add16(x, y));
    }

    private byte Add08(byte x, byte y, byte c = 0)
    {
      var temp = (ushort)(x + y + c);
      var result = (byte)temp;
      CarryFlag = (temp & 0xFF00) != 0;
      OverflowFlag = ((temp ^ x) & (temp ^ y) & 0x80) != 0;
      AuxiliaryCarryFlag = ((x ^ y ^ temp) & 0x10) != 0;
      SetFlagsFromValue(Width.Byte, temp);
      return result;
    }

    private ushort Add16(ushort x, ushort y, byte c = 0)
    {
      var temp = (uint)(x + y + c);
      var result = (ushort)temp;
      CarryFlag = (temp & 0xFFFF0000U) != 0;
      OverflowFlag = ((temp ^ x) & (temp ^ y) & 0x8000U) != 0;
      AuxiliaryCarryFlag = ((x ^ y ^ temp) & 0x10U) != 0;
      SetFlagsFromValue(Width.Word, result);
      return result;
    }

    private void And()
    {
      Debug("AND");
      HandleLogicalOpGroup((x, y) => (byte)(x & y), (x, y) => (ushort)(x & y));
    }

    private void DoCall(ushort offset)
    {
      Push(IP);
      IP = offset;
    }

    private void DoCall(ushort offset, ushort segment)
    {
      Push(CS);
      Push(IP);
      IP = offset;
      CS = segment;
    }

    private void Cbw()
    {
      Debug("CBW");
      AH = (AL & 0x80) == 0 ? (byte)0x00 : (byte)0xFF;

      clockCount += 2;
    }

    private void ChangeSegmentPrefix()
    {
      dataSegmentRegister = (SegmentRegister)((opcodes[0] >> 3) & 0b11);
      Debug($"{dataSegmentRegister}:");
      dataSegmentRegisterChanged = true;
    }

    private void Cmp()
    {
      Debug("CMP");
      HandleOpGroup((x, y) =>
      {
        Sub08(x, y);
        return null;
      }, (x, y) =>
      {
        Sub16(x, y);
        return null;
      });

      //TODO clockcount
    }

    private void Cmps()
    {
      Debug("CMPS");
      var width = OpWidth;
      var dst = ReadFromMemory(width, (ES << 4) + DI);
      var src = ReadFromMemory(width, (DataSegment << 4) + SI);
      if (width == Width.Byte) { Sub08((byte)src, (byte)dst); }
      else { Sub16(src, dst); }
      if (DirectionFlag)
      {
        SI -= (byte)width;
        DI -= (byte)width;
      }
      else
      {
        SI += (byte)width;
        DI += (byte)width;
      }
      var zf = ZeroFlag;
      if (repeat == Repeat.Positive && !zf || repeat == Repeat.Negative && zf)
      {
        repeat = Repeat.No;
      }

      clockCount += 22;
    }

    private void Cwd()
    {
      Debug("CWD");
      DX = (AX & 0x8000) == 0 ? (ushort)0x0000 : (ushort)0xFFFF;

      clockCount += 5;
    }

    private void Daa()
    {
      Debug("DAA");
      var oldal = AL;
      var oldcf = CarryFlag;
      CarryFlag = false;
      if ((AL & 0xF) > 9 || AuxiliaryCarryFlag)
      {
        AL += 6;
        CarryFlag = oldcf || (AL & 0x80) != 0;
        AuxiliaryCarryFlag = true;
      }
      else
      {
        AuxiliaryCarryFlag = false;
      }
      if (oldal > 0x99 || oldcf)
      {
        AL += 0x60;
        CarryFlag = true;
      }
      else
      {
        CarryFlag = false;
      }

      SetFlagsFromValue(Width.Byte, AL);

      clockCount += 4;
    }

    private void Das()
    {
      Debug("DAS");
      var oldal = AL;
      var oldcf = CarryFlag;
      CarryFlag = false;
      if ((AL & 0xF) > 9 || AuxiliaryCarryFlag)
      {
        AL -= 6;
        CarryFlag = oldcf || AL > 0;
        AuxiliaryCarryFlag = true;
      }
      else
      {
        AuxiliaryCarryFlag = false;
      }
      if (oldal > 0x99 || oldcf)
      {
        AL -= 0x60;
        CarryFlag = true;
      }
      else
      {
        CarryFlag = false;
      }
      SetFlagsFromValue(Width.Byte, AL);

      clockCount += 4;
    }

    private void DecRegister()
    {
      var regIndex = opcodes[0] & 0b111;
      Debug($"DEC {RegisterNames[regIndex]}");
      var oldCarry = CarryFlag;
      Registers[regIndex] = Sub16(Registers[regIndex], 1);
      CarryFlag = oldCarry;

      clockCount += 2;
    }

    private void Esc()
    {
      Debug("ESC");
      var (mod, _, rm) = ReadModRegRm();
      GetEffectiveAddress(mod, rm);

      clockCount = mod == 0b11 ? 2 : 8;
    }

    private void Hlt()
    {
      Debug("HLT");
      machine.Running = false;

      clockCount += 2;
    }

    private void IncRegister()
    {
      var regIndex = opcodes[0] & 0b111;
      Debug($"INC {RegisterNames[regIndex]}");
      var oldCarry = CarryFlag;
      Registers[regIndex] = Add16(Registers[regIndex], 1);
      CarryFlag = oldCarry;

      clockCount += 2;
    }

    private void Int(InterruptVector interruptVector)
    {
      Push(GetFlags());
      InterruptEnableFlag = TrapFlag = false;
      var tableOfs = (byte)interruptVector * 4;
      var ofs = memory.ReadWord(tableOfs);
      var segment = memory.ReadWord(tableOfs + 2);
      if (ofs == 0 && segment == 0) { throw new InvalidOperationException($"No handler for interrupt {(byte)interruptVector} found"); }
      DoCall(ofs, segment);

      clockCount += 51;
      if (opcodes[0] == 0xCC) { clockCount++; }
    }

    private void Into()
    {
      Debug("INTO");
      if (OverflowFlag)
      {
        Int(InterruptVector.Overflow);

        clockCount += 2; // +51 of INT 
      }

      clockCount += 4;
    }

    private void Iret()
    {
      Debug("IRET");
      IP = Pop();
      CS = Pop();
      SetFlags(Pop());

      clockCount += 24;
    }

    private void DoJmp(ushort offset, ushort segment)
    {
      CS = segment;
      IP = offset;
    }

    private void JumpShortConditional(bool condition, string mnemonic)
    {
      var relAddr = (sbyte)ReadCodeByte();
      Debug($"{mnemonic} {SignedHex(relAddr)}");
      if (condition)
      {
        IP = (ushort)(IP + relAddr);

        clockCount += 16 - 4;
      }

      clockCount += 4;
      if (mnemonic == "JCXZ") { clockCount += 2; }
    }

    private void Lahf()
    {
      Debug("LAHF");
      AH = (byte)GetFlags();

      clockCount += 4;
    }

    private void Lds()
    {
      Debug("LDS");
      var (mod, reg, rm) = ReadModRegRm();
      var addr = GetEffectiveAddress(mod, rm);
      Registers[reg] = memory.ReadWord(addr);
      DS = memory.ReadWord(addr + 2);

      clockCount += 16;
    }

    private void Lea()
    {
      Debug("LEA");
      var (mod, reg, rm) = ReadModRegRm();
      var addr = GetEffectiveAddress(mod, rm, false);
      Registers[reg] = (ushort)addr;

      clockCount += 2;
    }

    private void Les()
    {
      Debug("LES");
      var (mod, reg, rm) = ReadModRegRm();
      var addr = GetEffectiveAddress(mod, rm);
      Registers[reg] = memory.ReadWord(addr);
      ES = memory.ReadWord(addr + 2);

      clockCount += 16;
    }

    private void Lock()
    {
      Debug("LOCK");

      clockCount += 2;
    }

    private void Lods()
    {
      Debug("LODS");
      var width = OpWidth;
      SetRegisterValue(width, 0 /* AL or AX */, ReadFromMemory(width, (DataSegment << 4) + SI));
      if (DirectionFlag)
      {
        SI -= (byte)width;
      }
      else
      {
        SI += (byte)width;
      }

      clockCount += 12;
      if (repeat != Repeat.No) { clockCount++; }
    }

    private void Loop()
    {
      var relAddr = (sbyte)ReadCodeByte();
      Debug($"LOOP {SignedHex(relAddr)} (CX={CX})");
      if (--CX != 0)
      {
        IP = (ushort)(IP + relAddr);
        loopingCount++;

        clockCount += 17 - 5;
      }
      else
      {
        loopingCount = 0;
      }

      clockCount += 5;
    }

    private void LoopEqual()
    {
      var relAddr = (sbyte)ReadCodeByte();
      Debug($"LOOPE {SignedHex(relAddr)} (CX={CX})");
      if (--CX != 0 && ZeroFlag)
      {
        IP = (ushort)(IP + relAddr);
        loopingCount++;

        clockCount += 18 - 6;
      }
      else
      {
        loopingCount = 0;
      }

      clockCount += 6;
    }

    private void LoopNotEqual()
    {
      var relAddr = (sbyte)ReadCodeByte();
      Debug($"LOOPNE {SignedHex(relAddr)} (CX={CX})");
      if (--CX != 0 && !ZeroFlag)
      {
        IP = (ushort)(IP + relAddr);
        loopingCount++;

        clockCount += 19 - 5;
      }
      else
      {
        loopingCount = 0;
      }

      clockCount += 5;
    }

    private void MoveRegisterImmediate16()
    {
      var regIndex = opcodes[0] & 0b111;
      var value = ReadCodeWord();
      Debug($"MOV {RegisterNames[regIndex]},{value:X4}");
      Registers[regIndex] = value;

      clockCount += 4;
    }

    private void MoveRegisterImmediate08()
    {
      var regIndex = opcodes[0] & 0b111;
      var value = ReadCodeByte();
      Debug($"MOV {RegisterNames8[regIndex]},{value:X2}");
      SetRegister8(regIndex, value);

      clockCount += 4;
    }

    private void MovMemValue()
    {
      Debug("MOV");
      var width = OpWidth;
      var (mod, reg, rm) = ReadModRegRm();

      switch (reg)
      {
        case 0:
          WriteToRegisterOrMemory(width, mod, rm, () => width == Width.Byte ? ReadCodeByte() : ReadCodeWord());

          clockCount += mod == 0b11 ? 4 : 10;
          break;
        default:
          UnknownOpcode(mod, reg, rm);
          break;
      }
    }

    private void MovRegMem()
    {
      Debug("MOV");
      var width = OpWidth;
      var (mod, reg, rm) = ReadModRegRm();
      if (IsRegisterSource)
      {
        var src = GetRegisterValue(width, reg);
        DebugSourceThenTarget(GetRegisterName(width, reg));
        WriteToRegisterOrMemory(width, mod, rm, () => src);

        clockCount += mod == 0b11 ? 2 : 9;
      }
      else
      {
        var src = ReadFromRegisterOrMemory(width, mod, rm);
        DebugSourceThenTarget(GetRegisterName(width, reg));
        SetRegisterValue(width, reg, src);

        clockCount += mod == 0b11 ? 2 : 8;
      }
    }

    private void MovRegMemToSegReg()
    {
      Debug("MOV");
      var (mod, reg, rm) = ReadModRegRm();
      SetSegmentRegisterValue((SegmentRegister)reg, ReadFromRegisterOrMemory(Width.Word, mod, rm));
      DebugSourceThenTarget(((SegmentRegister)reg).ToString());

      clockCount += mod == 0b11 ? 2 : 8;
    }

    private void Movs()
    {
      Debug("MOVS");
      var width = OpWidth;
      var srcAddr = (DataSegment << 4) + SI;
      var dstAddr = (DataSegment << 4) + DI;

      if (width == Width.Byte)
      {
        memory.WriteByte(dstAddr, memory.ReadByte(srcAddr));
      }
      else
      {
        memory.WriteWord(dstAddr, memory.ReadWord(srcAddr));
      }

      if (DirectionFlag)
      {
        SI += (byte)width;
        DI += (byte)width;
      }
      else
      {
        SI -= (byte)width;
        DI -= (byte)width;
      }
    }

    private void MovSegRegToRegMem()
    {
      Debug("MOV");
      var (mod, reg, rm) = ReadModRegRm();
      DebugSourceThenTarget(((SegmentRegister)reg).ToString());
      WriteToRegisterOrMemory(Width.Word, mod, rm, () => GetSegmentRegisterValue((SegmentRegister)reg));

      clockCount += mod == 0b11 ? 2 : 9;
    }

    private void Nop()
    {
      Debug("NOP");

      clockCount += 3;
    }

    private void OpcodeGroup1()
    {
      // relOpcode = 0: XXX REG8/MEM8,IMMED8
      // relOpcode = 1: XXX REG16/MEM16,IMMED16
      // relOpcode = 2: XXX REG8/MEM8,IMMED8
      // relOpcode = 3: XXX REG16/MEM16,IMMED8

      var dstWidth = OpWidth;
      opcodes[0] &= 0b11;
      var srcWidth = opcodes[0] != 1 ? Width.Byte : Width.Word;

      var (mod, reg, rm) = ReadModRegRm();
      var dst = ReadFromRegisterOrMemory(dstWidth, mod, rm);
      var src = srcWidth == Width.Word ? ReadCodeWord() : ReadCodeByte();
      DebugSourceThenTarget(srcWidth == Width.Byte ? src.ToString("X2") : src.ToString("X4"));

      if (opcodes[0] == 3)
      {
        if ((src & 0x80) != 0) { src |= 0xFF00; }
      }

      var oldDebugParam = debugParam;
      switch (reg)
      {
        case 0:
        {
          Debug("ADD");
          debugParam = oldDebugParam;
          var result = dstWidth == Width.Byte ? Add08((byte)dst, (byte)src) : Add16(dst, src);
          WriteToRegisterOrMemory(dstWidth, mod, rm, () => result);
          break;
        }
        case 1:
        {
          Debug("OR");
          debugParam = oldDebugParam;
          var result = (ushort)(dst | src);
          SetFlagsForLogicalOp(dstWidth, result);
          WriteToRegisterOrMemory(dstWidth, mod, rm, () => result);
          break;
        }
        case 2:
        {
          Debug("ADC");
          var c = CarryFlag ? (byte)1 : (byte)0;
          var result = dstWidth == Width.Byte ? Add08((byte)dst, (byte)src, c) : Add16(dst, src, c);
          WriteToRegisterOrMemory(dstWidth, mod, rm, () => result);
          break;
        }
        case 3:
        {
          Debug("SBB");
          var c = CarryFlag ? (byte)1 : (byte)0;
          var result = dstWidth == Width.Byte ? Sub08((byte)dst, (byte)src, c) : Sub16(dst, src, c);
          WriteToRegisterOrMemory(dstWidth, mod, rm, () => result);
          break;
        }
        case 4:
        {
          Debug("AND");
          var result = (ushort)(dst & src);
          SetFlagsForLogicalOp(dstWidth, result);
          WriteToRegisterOrMemory(dstWidth, mod, rm, () => result);
          break;
        }
        case 5:
        {
          Debug("SUB");
          var result = dstWidth == Width.Byte ? Sub08((byte)dst, (byte)src) : Sub16(dst, src);
          WriteToRegisterOrMemory(dstWidth, mod, rm, () => result);
          break;
        }
        case 6:
        {
          Debug("XOR");
          var result = (ushort)(dst ^ src);
          SetFlagsForLogicalOp(dstWidth, result);
          WriteToRegisterOrMemory(dstWidth, mod, rm, () => result);
          break;
        }
        case 7:
        {
          Debug("CMP");
          if (dstWidth == Width.Byte) { Sub08((byte)dst, (byte)src); }
          else { Sub16(dst, src); }
          break;
        }
      }
      debugParam = oldDebugParam;

      clockCount += mod == 0b11 ? 4 : 17;
    }

    private void OpcodeGroup2()
    {
      var width = OpWidth;
      var (mod, reg, rm) = ReadModRegRm();

      var singleBitOpcode = (opcodes[0] & 0x2) == 0;
      byte bitCount = singleBitOpcode ? (byte)1 : CL;
      var effectiveBitCount = bitCount % 8;
      var dst = ReadFromRegisterOrMemory(width, mod, rm);
      var msb = width == Width.Byte ? (ushort)0x80 : (ushort)0x8000;
      bool tmpcf = CarryFlag;

      switch (reg)
      {
        case 0:
          Debug("ROL");
          if (effectiveBitCount == 0) { break; }

          for (var bit = 0; bit < effectiveBitCount; bit++)
          {
            tmpcf = (dst & msb) != 0;
            dst <<= 1;
            if (tmpcf) { dst |= 1; }
            CarryFlag = tmpcf;
          }
          OverflowFlag = tmpcf != ((dst & msb) != 0);
          break;
        case 1:
          Debug("ROR");
          if (effectiveBitCount == 0) { break; }

          for (var bit = 0; bit < effectiveBitCount; bit++)
          {
            tmpcf = (dst & 1) != 0;
            dst >>= 1;
            if (tmpcf) { dst |= msb; }
            CarryFlag = tmpcf;
          }
          OverflowFlag = tmpcf != ((dst & (msb >> 1)) != 0);
          break;
        case 2:
          Debug("RCL");
          if (effectiveBitCount == 0) { break; }

          for (var bit = 0; bit < effectiveBitCount; bit++)
          {
            tmpcf = CarryFlag;
            CarryFlag = (dst & msb) != 0;
            dst <<= 1;
            if (tmpcf) { dst |= 1; }
          }
          OverflowFlag = CarryFlag != ((dst & msb) != 0);
          break;
        case 3:
          Debug("RCR");
          if (effectiveBitCount == 0) { break; }

          for (var bit = 0; bit < effectiveBitCount; bit++)
          {
            tmpcf = CarryFlag;
            CarryFlag = (dst & 1) != 0;
            dst >>= 1;
            if (tmpcf) { dst |= msb; }
          }
          OverflowFlag = tmpcf != ((dst & (msb >> 1)) != 0);
          break;
        case 4:
          Debug("SHL");
          if (effectiveBitCount == 0) { break; }

          for (var bit = 0; bit < effectiveBitCount; bit++)
          {
            tmpcf = (dst & msb) != 0;
            dst <<= 1;
            SetFlagsForLogicalOp(width, dst);
            CarryFlag = tmpcf;
          }
          OverflowFlag = tmpcf != ((dst & msb) != 0);
          break;
        case 5:
          Debug("SHR");
          if (effectiveBitCount == 0) { break; }

          for (var bit = 0; bit < effectiveBitCount; bit++)
          {
            tmpcf = (dst & 1) != 0;
            dst >>= 1;
            SetFlagsForLogicalOp(width, dst);
            CarryFlag = tmpcf;
          }
          OverflowFlag = (dst & msb) != 0 != ((dst & (msb >> 1)) != 0);
          break;
        case 7:
          Debug("SAR");
          if (effectiveBitCount == 0) { break; }

          for (var bit = 0; bit < effectiveBitCount; bit++)
          {
            tmpcf = (dst & 1) != 0;
            var sign = (ushort)(dst & msb);
            dst = (ushort)(((dst & ~msb) >> 1) | sign);
            SetFlagsForLogicalOp(width, dst);
            CarryFlag = tmpcf;
          }
          break;
        default:
          UnknownOpcode(mod, reg, rm);
          return;
      }
      WriteToRegisterOrMemory(width, mod, rm, () => dst);

      if (singleBitOpcode)
      {
        clockCount += mod == 0b11 ? 2 : 15;
      }
      else
      {
        clockCount += (mod == 0b11 ? 8 : 20) + 4 * bitCount;
      }
    }

    private void OpcodeGroup3()
    {
      var width = OpWidth;
      var (mod, reg, rm) = ReadModRegRm();
      var src = ReadFromRegisterOrMemory(width, mod, rm);
      ushort dst;

      switch (reg)
      {
        case 0:
          Debug("TEST");
          dst = width == Width.Byte ? ReadCodeByte() : ReadCodeWord();
          SetFlagsForLogicalOp(width, (ushort)(dst & src));

          clockCount += mod == 0b11 ? 5 : 11;
          break;
        case 1:
          Debug("NOT");
          WriteToRegisterOrMemory(width, mod, rm, () => (ushort)~src);

          clockCount += mod == 0b11 ? 3 : 16;
          break;
        case 2:
          Debug("NEG");
          if (width == Width.Byte)
          {
            if (src == 0xFF) { OverflowFlag = true; }
            else
            {
              WriteToRegisterOrMemory(width, mod, rm, () => Sub08(0, (byte)src));
            }
          }
          else
          {
            if (src == 0xFFFF) { OverflowFlag = true; }
            else
            {
              WriteToRegisterOrMemory(width, mod, rm, () => Sub16(0, src));
            }
          }
          CarryFlag = src > 0;

          clockCount += mod == 0b11 ? 3 : 16;
          break;
        case 3:
          Debug("MUL");
          if (width == Width.Byte)
          {
            AX = (ushort)(AL * src);
            CarryFlag = OverflowFlag = AH > 0;

            clockCount += mod == 0b11 ? (77 - 70) / 2 : (83 - 76) / 2;
          }
          else
          {
            var result = AX * src;
            AX = (ushort)result;
            DX = (ushort)(result >> 16);
            CarryFlag = OverflowFlag = DX > 0;

            clockCount += mod == 0b11 ? (133 - 118) / 2 : (139 - 124) / 2;
          }
          break;
        case 4:
          Debug("IMUL");
          if (width == Width.Byte)
          {
            AX = (ushort)((sbyte)AL * (sbyte)src);
            CarryFlag = OverflowFlag = AH > 0 && AH < 0xFF;

            clockCount += mod == 0b11 ? (98 - 80) / 2 : (154 - 128) / 2;
          }
          else
          {
            var result = (short)AX * (short)src;
            AX = (ushort)result;
            DX = (ushort)(result >> 16);
            CarryFlag = OverflowFlag = DX > 0 && DX < 0xFFFF;

            clockCount += mod == 0b11 ? (104 - 86) / 2 : (160 - 134) / 2;
          }
          break;
        case 5:
          Debug("DIV");
          if (src == 0)
          {
            Int(InterruptVector.DivideByZero);
            return;
          }

          if (width == Width.Byte)
          {
            dst = AX;
            var result = (ushort)(dst / src);
            if (result > byte.MaxValue) { Int(InterruptVector.DivideByZero); }
            else
            {
              AL = (byte)result;
              AH = (byte)(dst % src);
            }

            clockCount += mod == 0b11 ? (90 - 80) / 2 : (96 - 86) / 2;
          }
          else
          {
            var longdst = (uint)((DX << 16) | AX);
            var result = longdst / src;
            if (result > ushort.MaxValue) { Int(InterruptVector.DivideByZero); }
            else
            {
              AX = (ushort)result;
              DX = (ushort)(longdst % src);
            }

            clockCount += mod == 0b11 ? (162 - 144) / 2 : (168 - 150) / 2;
          }
          break;
        case 6:
          Debug("IDIV");
          if (src == 0)
          {
            Int(InterruptVector.DivideByZero);
            return;
          }

          if (width == Width.Byte)
          {
            var ssrc = (sbyte)src;
            var sdst = (short)AX;
            var result = sdst / ssrc;
            if (result > sbyte.MaxValue || result < sbyte.MinValue) { Int(InterruptVector.DivideByZero); }
            else
            {
              AL = (byte)result;
              AH = (byte)(sdst % ssrc);
            }

            clockCount += mod == 0b11 ? (112 - 101) / 2 : (118 - 107) / 2;
          }
          else
          {
            var ssrc = (short)src;
            var sdst = (DX << 16) | AX;
            var result = sdst / ssrc;
            if (result > short.MaxValue || result < short.MinValue) { Int(InterruptVector.DivideByZero); }
            else
            {
              AX = (ushort)result;
              DX = (ushort)(sdst % src);
            }

            clockCount += mod == 0b11 ? (184 - 165) / 2 : (190 - 171) / 2;
          }
          break;
        default:
          UnknownOpcode(mod, reg, rm);
          break;
      }
    }

    private void OpcodeGroup4()
    {
      var width = OpWidth;
      var (mod, reg, rm) = ReadModRegRm();
      var src = ReadFromRegisterOrMemory(width, mod, rm);
      var oldCarry = CarryFlag;
      switch (reg)
      {
        case 0:
          Debug("INC");
          var incResult = width == Width.Byte ? Add08((byte)src, 1) : Add16(src, 1);
          WriteToRegisterOrMemory(width, mod, rm, () => incResult);
          CarryFlag = oldCarry;

          clockCount += mod == 0b11 ? 3 : 15;
          break;
        case 1:
          Debug("DEC");
          var decResult = width == Width.Byte ? Sub08((byte)src, 1) : Sub16(src, 1);
          WriteToRegisterOrMemory(width, mod, rm, () => decResult);
          CarryFlag = oldCarry;

          clockCount += mod == 0b11 ? 3 : 15;
          break;
        default:
          UnknownOpcode(mod, reg, rm);
          break;
      }
    }

    private void OpcodeGroup5()
    {
      var (mod, reg, rm) = ReadModRegRm();
      var src = ReadFromRegisterOrMemory(Width.Word, mod, rm);

      var oldCarry = CarryFlag;
      switch (reg)
      {
        case 0:
          Debug("INC");
          WriteToRegisterOrMemory(Width.Word, mod, rm, () => Add16(src, 1));
          CarryFlag = oldCarry;

          clockCount += mod == 0b11 ? 3 : 15;
          break;
        case 1:
          Debug("DEC");
          WriteToRegisterOrMemory(Width.Word, mod, rm, () => Sub16(src, 1));
          CarryFlag = oldCarry;

          clockCount += mod == 0b11 ? 3 : 15;
          break;
        case 2:
          Debug("CALL");
          DoCall(src);

          clockCount += mod == 0b11 ? 16 : 21;
          break;
        case 3:
        {
          Debug("CALL");
          if (currentEffectiveAddress == null) // mod == 0b11
          {
            UnknownOpcode(mod, reg, rm);
            break;
          }
          DoCall(memory.ReadWord(currentEffectiveAddress.Value), memory.ReadWord(currentEffectiveAddress.Value + 2));

          clockCount += 37;
          break;
        }
        case 4:
          Debug("JMP");
          IP = src;

          clockCount += mod == 0b11 ? 11 : 18;
          break;
        case 5:
        {
          Debug("JMP");
          if (currentEffectiveAddress == null) // mod == 0b11
          {
            UnknownOpcode(mod, reg, rm);
            break;
          }
          DoJmp(memory.ReadWord(currentEffectiveAddress.Value), memory.ReadWord(currentEffectiveAddress.Value + 2));

          clockCount += 24;
          break;
        }
        case 6:
          Debug("PUSH");
          Push(src);

          clockCount += mod == 0b11 ? 11 : 16;
          break;
        default:
          UnknownOpcode(mod, reg, rm);
          break;
      }
    }

    private void Or()
    {
      Debug("OR");
      HandleOpGroup((x, y) => (byte)(x | y), (x, y) => (ushort)(x | y));
    }

    private ushort Pop()
    {
      var value = memory.ReadWord((SS << 4) + SP);
      SP += 2;
      return value;
    }

    private void PopFlags()
    {
      Debug("POPF");
      SetFlags(Pop());
    }

    private void PopRegister()
    {
      var regIndex = opcodes[0] & 0b111;
      Debug($"POP {RegisterNames[regIndex]}");
      Registers[regIndex] = Pop();

      clockCount += 8;
    }

    private void PopRegMem()
    {
      Debug("POP");
      var (mod, reg, rm) = ReadModRegRm();
      switch (reg)
      {
        case 0:
          Debug("POP");
          WriteToRegisterOrMemory(Width.Word, mod, rm, Pop);

          clockCount += mod == 0b11 ? 8 : 17;
          break;
        default:
          UnknownOpcode(mod, reg, rm);
          break;
      }
    }

    private void PopSegment()
    {
      var segmentRegister = (SegmentRegister)((opcodes[0] >> 3) & 0b11);
      Debug($"POP {segmentRegister}");
      SetSegmentRegisterValue(segmentRegister, Pop());

      clockCount += 8;
    }

    private byte PortIn08(ushort port, bool debug = true)
    {
      if (debug) { Debug($"IN AL,{port:X2}"); }

      return machine.Ports.TryGetValue(port, out var handler) ? handler.GetByte(port) : (byte)0;
    }

    private ushort PortIn16(ushort port, bool debug = true)
    {
      if (debug) { Debug($"IN AX,{port:X2}"); }

      if (machine.Ports.TryGetValue(port, out var handler))
      {
        if (handler is I16BitInternalDevice wordHandler)
        {
          return wordHandler.GetWord(port);
        }

        return (ushort)(handler.GetByte(port) | (handler.GetByte(port + 1) << 8));
      }

      return 0;
    }

    private void PortOut08(ushort port, bool debug = true)
    {
      if (debug) { Debug($"OUT AL,{port:X2}"); }

      if (machine.Ports.TryGetValue(port, out var handler))
      {
        handler.SetByte(port, AL);
      }
    }

    private void PortOut16(ushort port, bool debug = true)
    {
      if (debug) { Debug($"OUT AX,{port:X2}"); }

      if (machine.Ports.TryGetValue(port, out var handler))
      {
        if (handler is I16BitInternalDevice wordHandler)
        {
          wordHandler.SetWord(port, AX);
        }
        else
        {
          handler.SetByte(port, AL);
          handler.SetByte(port + 1, AH);
        }
      }
    }

    private void Push(ushort value)
    {
      SP -= 2;
      memory.WriteWord((SS << 4) + SP, value);
    }

    private void PushFlags()
    {
      Debug("PUSHF");
      Push(GetFlags());

      clockCount += 10;
    }

    private void PushRegister()
    {
      var regIndex = opcodes[0] & 0b111;
      Debug($"PUSH {RegisterNames[regIndex]}");
      Push(Registers[regIndex]);

      clockCount += 11;
    }

    private void PushSegment()
    {
      var segmentRegister = (SegmentRegister)((opcodes[0] >> 3) & 0b11);
      Debug($"PUSH {segmentRegister}");
      Push(GetSegmentRegisterValue(segmentRegister));

      clockCount += 10;
    }

    // ReSharper disable once InconsistentNaming
    private void RepeatCX(Action action)
    {
      if (repeat != Repeat.No && CX == 0)
      {
        repeat = Repeat.No;
        return;
      }

      action();

      if (repeat != Repeat.No)
      {
        if (--CX == 0) { repeat = Repeat.No; }
        else { IP -= opcodeIndex; }
      }
    }

    private void RetInterSeg(ushort? stackChange = null)
    {
      Debug($"RET {stackChange}");
      IP = Pop();
      CS = Pop();
      if (stackChange.HasValue)
      {
        SP += stackChange.Value;

        clockCount += 17;
      }
      else
      {
        clockCount += 18;
      }
    }

    private void RetIntraSeg(ushort? stackChange = null)
    {
      Debug($"RET {stackChange}");
      IP = Pop();
      if (stackChange.HasValue)
      {
        SP += stackChange.Value;

        clockCount += 12;
      }
      else
      {
        clockCount += 8;
      }
    }

    private void Sahf()
    {
      Debug("SAHF");
      SetFlags(AH);

      clockCount += 4;
    }

    private void Sbb()
    {
      Debug("SBB");
      var c = CarryFlag ? (byte)1 : (byte)0;
      HandleOpGroup((x, y) => Sub08(x, y, c), (x, y) => Sub16(x, y, c));
    }

    private void Scas()
    {
      Debug("SCAS");
      var width = OpWidth;
      var dst = ReadFromMemory(width, (ES << 4) + DI);
      if (width == Width.Byte) { Sub08(AL, (byte)dst); }
      else { Sub16(AX, dst); }
      if (DirectionFlag) { DI -= (byte)width; }
      else { DI += (byte)width; }

      var zf = ZeroFlag;
      if (repeat == Repeat.Positive && !zf || repeat == Repeat.Negative && zf)
      {
        repeat = Repeat.No;
      }

      clockCount += 15;
    }

    private void Stos()
    {
      Debug("STOS");
      var width = OpWidth;
      WriteToMemory(width, (DataSegment << 4) + DI, GetRegisterValue(width, 0 /* AL or AX */));
      if (DirectionFlag)
      {
        SI -= (byte)width;
      }
      else
      {
        SI += (byte)width;
      }

      clockCount += repeat == Repeat.No ? 11 : 10;
    }

    private void Sub()
    {
      Debug("SUB");
      HandleOpGroup((x, y) => Sub08(x, y), (x, y) => Sub16(x, y));
    }

    private byte Sub08(byte x, byte y, byte c = 0)
    {
      var temp = (ushort)(x - y - c);
      var result = (byte)temp;
      CarryFlag = (temp & 0xFF00) != 0;
      OverflowFlag = ((temp ^ x) & (temp ^ y) & 0x80) != 0;
      AuxiliaryCarryFlag = ((x ^ y ^ temp) & 0x10) != 0;
      SetFlagsFromValue(Width.Byte, temp);
      return result;
    }

    private ushort Sub16(ushort x, ushort y, byte c = 0)
    {
      var temp = (uint)(x - y - c);
      var result = (ushort)temp;
      CarryFlag = (temp & 0xFFFF0000U) != 0;
      OverflowFlag = ((temp ^ x) & (temp ^ y) & 0x8000U) != 0;
      AuxiliaryCarryFlag = ((x ^ y ^ temp) & 0x10U) != 0;
      SetFlagsFromValue(Width.Word, result);
      return result;
    }

    private void Test(ushort regValue, ushort immValue)
    {
      var width = OpWidth;
      Debug(width == Width.Byte ? $"TEST AL,{immValue:X2}" : $"TEST AX,{immValue:X4}");

      SetFlagsForLogicalOp(width, (ushort)(regValue & immValue));

      clockCount += 5;
    }

    private void Test()
    {
      Debug("TEST");
      var width = OpWidth;
      var (mod, reg, rm) = ReadModRegRm();
      var dst = ReadFromRegisterOrMemory(width, mod, rm);
      var src = GetRegisterValue(width, reg);
      SetFlagsForLogicalOp(width, (ushort)(dst & src));

      clockCount += 9;
    }

    private void Wait()
    {
      Debug("WAIT");

      clockCount += 3; // + 5n
    }

    private void Xchg()
    {
      Debug("XCHG");
      var width = OpWidth;
      var (mod, reg, rm) = ReadModRegRm();

      var src = ReadFromRegisterOrMemory(width, mod, rm);
      var dst = GetRegisterValue(width, reg);
      DebugSourceThenTarget(GetRegisterName(width, reg));

      SetRegisterValue(width, reg, src);
      WriteToRegisterOrMemory(width, mod, rm, () => dst);

      clockCount += 17;
    }

    private void XchgAx()
    {
      var regIndex = opcodes[0] & 0b111;
      Debug($"XCHG AX,{RegisterNames[regIndex]}");
      var tmp = AX;
      AX = Registers[regIndex];
      Registers[regIndex] = tmp;

      clockCount += 3;
    }

    private void Xlat()
    {
      Debug("XLAT");
      AL = memory.ReadByte((DataSegment << 4) + (ushort)(BX + AL));

      clockCount += 11;
    }

    private void Xor()
    {
      Debug("XOR");
      HandleLogicalOpGroup((x, y) => (byte)(x ^ y), (x, y) => (ushort)(x ^ y));
    }
  }
}