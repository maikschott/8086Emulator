using System;
using Masch.Emulator8086.InternalDevices;

namespace Masch.Emulator8086.CPU
{
  public partial class Cpu8086
  {
    protected void DoBitShift(Width width, byte mod, byte reg, byte rm, ushort dst, byte bitCount)
    {
      bitCount &= 0x1F;
      var msb = width == Width.Byte ? (ushort)0x80 : (ushort)0x8000;
      bool tmpcf;
      var wasSigned = (dst & msb) != 0;

      switch (reg)
      {
        case 0:
          SetDebug("ROL");
          if (bitCount == 0) { break; }

          for (var bit = 0; bit < bitCount; bit++)
          {
            tmpcf = (dst & msb) != 0;
            dst <<= 1;
            if (tmpcf) { dst |= 1; }
            CarryFlag = tmpcf;
          }
          break;
        case 1:
          SetDebug("ROR");
          if (bitCount == 0) { break; }

          for (var bit = 0; bit < bitCount; bit++)
          {
            tmpcf = (dst & 1) != 0;
            dst >>= 1;
            if (tmpcf) { dst |= msb; }
            CarryFlag = tmpcf;
          }
          break;
        case 2:
          SetDebug("RCL");
          if (bitCount == 0) { break; }

          for (var bit = 0; bit < bitCount; bit++)
          {
            tmpcf = CarryFlag;
            CarryFlag = (dst & msb) != 0;
            dst <<= 1;
            if (tmpcf) { dst |= 1; }
          }
          break;
        case 3:
          SetDebug("RCR");
          if (bitCount == 0) { break; }

          for (var bit = 0; bit < bitCount; bit++)
          {
            tmpcf = CarryFlag;
            CarryFlag = (dst & 1) != 0;
            dst >>= 1;
            if (tmpcf) { dst |= msb; }
          }
          break;
        case 4:
        case 6: // undocumented
          SetDebug(reg == 4 ? "SHL" : "SAL");
          if (bitCount == 0) { break; }

          for (var bit = 0; bit < bitCount; bit++)
          {
            tmpcf = (dst & msb) != 0;
            dst <<= 1;
            SetFlagsForLogicalOp(width, dst);
            CarryFlag = tmpcf;
          }
          break;
        case 5:
          SetDebug("SHR");
          if (bitCount == 0) { break; }

          for (var bit = 0; bit < bitCount; bit++)
          {
            tmpcf = (dst & 1) != 0;
            dst >>= 1;
            SetFlagsForLogicalOp(width, dst);
            CarryFlag = tmpcf;
          }
          break;
        case 7:
          SetDebug("SAR");
          if (bitCount == 0) { break; }

          var sign = (ushort)(dst & msb);
          var mask = ~msb;

          for (var bit = 0; bit < bitCount; bit++)
          {
            tmpcf = (dst & 1) != 0;
            dst = (ushort)(((dst & mask) >> 1) | sign);
            SetFlagsForLogicalOp(width, dst);
            CarryFlag = tmpcf;
          }
          break;
        default:
          UnknownOpcode(mod, reg, rm);
          return;
      }
      WriteToRegisterOrMemory(width, mod, rm, () => dst);

      var isSigned = (dst & msb) != 0;
      OverflowFlag = wasSigned != isSigned;
    }

    protected override void DoInt(InterruptVector interruptVector, Action flagAction = null)
    {
      Push(GetFlags());
      flagAction?.Invoke();
      var tableOfs = (byte)interruptVector * 4;
      DoCall(memory.ReadWord(tableOfs), memory.ReadWord(tableOfs + 2));

      clockCount += 51;
      if (opcodes[0] == 0xCC) { clockCount++; }
    }

    protected virtual void Esc()
    {
      SetDebug("ESC");
      var (mod, _, rm) = ReadModRegRm();
      GetEffectiveAddress(mod, rm);

      clockCount = mod == 0b11 ? 2 : 8;
    }

    protected (ushort hi, ushort lo) Imul16(short op1, short op2)
    {
      var result = op1 * op2;
      var lo = (ushort)result;
      var hi = (ushort)(result >> 16);
      CarryFlag = OverflowFlag = hi > 0 && hi < 0xFFFF;

      return (hi, lo);
    }

    protected ushort Pop()
    {
      var value = memory.ReadWord((SS << 4) + SP);
      SP += 2;
      return value;
    }

    protected byte PortIn08(ushort port, bool setDebug = true)
    {
      if (setDebug) { SetDebug("IN", "AL", port.ToString("X2")); }

      return machine.Ports.TryGetValue(port, out var handler) ? handler.GetByte(port) : (byte)0;
    }

    protected ushort PortIn16(ushort port, bool setDebug = true)
    {
      if (setDebug) { SetDebug("IN", "AX", port.ToString("X2")); }

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

    protected void PortOut08(ushort port, byte value, bool setDebug = true)
    {
      if (setDebug) { SetDebug("OUT", port.ToString("X2"), "AL"); }

      if (machine.Ports.TryGetValue(port, out var handler)) { handler.SetByte(port, value); }
    }

    protected void PortOut16(ushort port, ushort value, bool setDebug = true)
    {
      if (setDebug) { SetDebug("OUT", port.ToString("X2"), "AX"); }

      if (machine.Ports.TryGetValue(port, out var handler))
      {
        if (handler is I16BitInternalDevice wordHandler)
        {
          wordHandler.SetWord(port, value);
        }
        else
        {
          handler.SetByte(port, (byte)value);
          handler.SetByte(port + 1, (byte)(value >> 8));
        }
      }
    }

    protected void Push(ushort value)
    {
      SP -= 2;
      memory.WriteWord((SS << 4) + SP, value);
    }

    // ReSharper disable once InconsistentNaming
    protected void RepeatCX(Action action)
    {
      if (repeatWhileNotZero != null)
      {
        if (CX == 0)
        {
          repeatWhileNotZero = null;
          return;
        }
      }

      action();

      if (repeatWhileNotZero != null)
      {
        if (--CX == 0) { repeatWhileNotZero = null; }
        else { IP -= opcodeIndex; }
      }
    }

    private void Aaa()
    {
      SetDebug("AAA");
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
      SetDebug("AAD");
      // value != 10 was undocumented up until the Pentium
      var value = ReadCodeByte();
      if (value != 10) { SetDebugSourceThenTarget(value.ToString("X2")); }
      AX = (byte)(AH * value + AL);
      SetFlagsFromValue(Width.Byte, AL);

      clockCount += 60;
    }

    private void Aam()
    {
      SetDebug("AAM");
      var value = ReadCodeByte();
      if (value != 10) { SetDebugSourceThenTarget(value.ToString("X2")); }
      if (value == 0)
      {
        DoInt(InterruptVector.CpuDivideByZero);
      }
      else
      {
        // value != 10 was undocumented up until the Pentium
        AH = (byte)(AL / value);
        AL = (byte)(AL % value);
        SetFlagsFromValue(Width.Word, AX);
      }

      clockCount += 83;
    }

    private void Aas()
    {
      SetDebug("AAS");
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
      SetDebug("ADC");
      var c = CarryFlag ? (byte)1 : (byte)0;
      HandleOpGroup((x, y) => Add08(x, y, c), (x, y) => Add16(x, y, c));
    }

    private void Add()
    {
      SetDebug("ADD");
      HandleOpGroup((x, y) => Add08(x, y), (x, y) => Add16(x, y));
    }

    private byte Add08(byte op1, byte op2, byte carry = 0)
    {
      var temp = (ushort)(op1 + op2 + carry);
      var result = (byte)temp;
      CarryFlag = (temp & 0xFF00) != 0;
      OverflowFlag = ((temp ^ op1) & (temp ^ op2) & 0x80) != 0;
      AuxiliaryCarryFlag = ((op1 ^ op2 ^ temp) & 0x10) != 0;
      SetFlagsFromValue(Width.Byte, result);
      return result;
    }

    private ushort Add16(ushort op1, ushort op2, byte carry = 0)
    {
      var temp = (uint)(op1 + op2 + carry);
      var result = (ushort)temp;
      CarryFlag = (temp & 0xFFFF0000U) != 0;
      OverflowFlag = ((temp ^ op1) & (temp ^ op2) & 0x8000U) != 0;
      AuxiliaryCarryFlag = ((op1 ^ op2 ^ temp) & 0x10U) != 0;
      SetFlagsFromValue(Width.Word, result);
      return result;
    }

    private void And()
    {
      SetDebug("AND");
      HandleLogicalOpGroup((x, y) => x & y);
    }

    private void Cbw()
    {
      SetDebug("CBW");
      AX = (ushort)(sbyte)AL;

      clockCount += 2;
    }

    private void ChangeSegmentPrefix()
    {
      dataSegmentRegister = (SegmentRegister)((opcodes[0] >> 3) & 0b11);
      dataSegmentRegisterChanged = true;
      SetDebug($"{dataSegmentRegister}:");
    }

    private void Cmp()
    {
      SetDebug("CMP");
      var mod = HandleOpGroup((x, y) =>
      {
        Sub08(x, y);
        return null;
      }, (x, y) =>
      {
        Sub16(x, y);
        return null;
      });

      clockCount += mod == 0b11 ? 3 : 9;
    }

    private void Cmps()
    {
      var width = OpWidth;

      if (width == Width.Byte)
      {
        SetDebug("CMPSB");
        var dst = memory.ReadByte((ES << 4) + DI);
        var src = ReadDataByte(SI);
        Sub08(src, dst);
      }
      else
      {
        SetDebug("CMPSW");
        var dst = memory.ReadWord((ES << 4) + DI);
        var src = ReadDataWord(SI);
        Sub16(src, dst);
      }

      SI += GetSourceOrDestDelta(width);
      DI += GetSourceOrDestDelta(width);

      if (repeatWhileNotZero == ZeroFlag) { repeatWhileNotZero = null; }

      clockCount += 22;
    }

    private void Cwd()
    {
      SetDebug("CWD");
      var doubleWord = (uint)(short)AX;
      DX = (ushort)(doubleWord >> 16);

      clockCount += 5;
    }

    private void Daa()
    {
      SetDebug("DAA");
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
      SetDebug("DAS");
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
      SetDebug("DEC", RegisterNames[regIndex]);
      var oldCarry = CarryFlag;
      Registers[regIndex] = Sub16(Registers[regIndex], 1);
      CarryFlag = oldCarry;

      clockCount += 2;
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

    private void DoJmp(ushort offset, ushort segment)
    {
      CS = segment;
      IP = offset;
    }

    private void Hlt()
    {
      loop = null;
      SetDebug("HLT");
      machine.Stop();

      clockCount += 2;
    }

    private void IncRegister()
    {
      var regIndex = opcodes[0] & 0b111;
      SetDebug("INC", RegisterNames[regIndex]);
      var oldCarry = CarryFlag;
      Registers[regIndex] = Add16(Registers[regIndex], 1);
      CarryFlag = oldCarry;

      clockCount += 2;
    }

    private void Into()
    {
      SetDebug("INTO");
      if (OverflowFlag)
      {
        DoInt(InterruptVector.CpuOverflow);

        clockCount += 2; // +51 of INT 
      }

      clockCount += 4;
    }

    private void Iret()
    {
      SetDebug("IRET");
      IP = Pop();
      CS = Pop();
      SetFlags(Pop());

      clockCount += 24;

      irqReturnAction?.Invoke();
      irqReturnAction = null;
    }

    private void JumpShortConditional(bool condition, string mnemonic)
    {
      var relAddr = (sbyte)ReadCodeByte();
      SetDebug(mnemonic, SignedHex(relAddr));
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
      SetDebug("LAHF");
      AH = (byte)GetFlags();

      clockCount += 4;
    }

    private void Lds()
    {
      SetDebug("LDS");
      var (mod, reg, rm) = ReadModRegRm();
      var addr = GetEffectiveAddress(mod, rm);
      Registers[reg] = memory.ReadWord(addr);
      DS = memory.ReadWord(addr + 2);

      clockCount += 16;
    }

    private void Lea()
    {
      SetDebug("LEA");
      var (mod, reg, rm) = ReadModRegRm();
      var addr = GetEffectiveAddress(mod, rm, false);
      Registers[reg] = (ushort)addr;

      clockCount += 2;
    }

    private void Les()
    {
      SetDebug("LES");
      var (mod, reg, rm) = ReadModRegRm();
      var addr = GetEffectiveAddress(mod, rm);
      Registers[reg] = memory.ReadWord(addr);
      ES = memory.ReadWord(addr + 2);

      clockCount += 16;
    }

    private void Lock()
    {
      SetDebug("LOCK");

      clockCount += 2;
    }

    private void Lods()
    {
      var width = OpWidth;
      if (width == Width.Byte)
      {
        SetDebug("LODSB");
        AL = ReadDataByte(SI);
      }
      else
      {
        SetDebug("LODSW");
        AX = ReadDataWord(SI);
      }

      SI += GetSourceOrDestDelta(width);

      clockCount += 12;
      if (repeatWhileNotZero != null) { clockCount++; }
    }

    private void Loop()
    {
      var relAddr = (sbyte)ReadCodeByte();
      SetDebug("LOOP", $"{SignedHex(relAddr)} (CX={CX:X4})");
      if (--CX != 0)
      {
        var loopStart = IP;
        IP = (ushort)(IP + relAddr);
        loop = relAddr > 0 ? (loopStart, IP) : (IP, loopStart);

        clockCount += 17 - 5;
      }
      else
      {
        loop = null;
      }

      clockCount += 5;
    }

    private void LoopEqual()
    {
      var relAddr = (sbyte)ReadCodeByte();
      SetDebug("LOOPE", $"{SignedHex(relAddr)} (CX={CX:X4})");
      if (--CX != 0 && ZeroFlag)
      {
        var loopStart = IP;
        IP = (ushort)(IP + relAddr);
        loop = relAddr > 0 ? (loopStart, IP) : (IP, loopStart);

        clockCount += 18 - 6;
      }
      else
      {
        loop = null;
      }

      clockCount += 6;
    }

    private void LoopNotEqual()
    {
      var relAddr = (sbyte)ReadCodeByte();
      SetDebug("LOOPNE", $"{SignedHex(relAddr)} (CX={CX:X4})");
      if (--CX != 0 && !ZeroFlag)
      {
        var loopStart = IP;
        IP = (ushort)(IP + relAddr);
        loop = relAddr > 0 ? (loopStart, IP) : (IP, loopStart);

        clockCount += 19 - 5;
      }
      else
      {
        loop = null;
      }

      clockCount += 5;
    }

    private void MoveRegisterImmediate08()
    {
      var regIndex = opcodes[0] & 0b111;
      var value = ReadCodeByte();
      SetDebug("MOV", RegisterNames8[regIndex], value.ToString("X2"));
      SetRegister8(regIndex, value);

      clockCount += 4;
    }

    private void MoveRegisterImmediate16()
    {
      var regIndex = opcodes[0] & 0b111;
      var value = ReadCodeWord();
      SetDebug("MOV", RegisterNames[regIndex], value.ToString("X4"));
      Registers[regIndex] = value;

      clockCount += 4;
    }

    private void MovMemValue()
    {
      SetDebug("MOV");
      var width = OpWidth;
      var (mod, reg, rm) = ReadModRegRm();

      switch (reg)
      {
        case 0:
          WriteToRegisterOrMemory(width, mod, rm, () =>
          {
            if (width == Width.Byte)
            {
              var value = ReadCodeByte();
              SetDebugSourceThenTarget(value.ToString("X2"));
              return value;
            }
            else
            {
              var value = ReadCodeWord();
              SetDebugSourceThenTarget(value.ToString("X4"));
              return value;
            }
          });

          clockCount += mod == 0b11 ? 4 : 10;
          break;
        default:
          UnknownOpcode(mod, reg, rm);
          break;
      }
    }

    private void MovRegMem()
    {
      SetDebug("MOV");
      var width = OpWidth;
      var (mod, reg, rm) = ReadModRegRm();
      if (IsRegisterSource)
      {
        var src = GetRegisterValue(width, reg);
        SetDebugSourceThenTarget(GetRegisterName(width, reg));
        WriteToRegisterOrMemory(width, mod, rm, () => src);

        clockCount += mod == 0b11 ? 2 : 9;
      }
      else
      {
        var src = ReadFromRegisterOrMemory(width, mod, rm);
        SetDebugSourceThenTarget(GetRegisterName(width, reg));
        SetRegisterValue(width, reg, src);

        clockCount += mod == 0b11 ? 2 : 8;
      }
    }

    private void MovRegMemToSegReg()
    {
      SetDebug("MOV");
      var (mod, reg, rm) = ReadModRegRm();
      var segReg = (SegmentRegister)reg;
      if (segReg == SegmentRegister.CS && this is Cpu80186) // only allowed on the 8086
      {
        UnknownOpcode(mod, reg, rm);
        return;
      }
      SetSegmentRegisterValue(segReg, ReadFromRegisterOrMemory(Width.Word, mod, rm));
      SetDebugSourceThenTarget(segReg.ToString());

      clockCount += mod == 0b11 ? 2 : 8;
    }

    private void Movs()
    {
      var width = OpWidth;
      var dstAddr = (ES << 4) + DI;

      if (width == Width.Byte)
      {
        SetDebug($"MOVSB");
        memory.WriteByte(dstAddr, ReadDataByte(SI));
      }
      else
      {
        SetDebug($"MOVSW");
        memory.WriteWord(dstAddr, ReadDataWord(SI));
      }

      SI += GetSourceOrDestDelta(width);
      DI += GetSourceOrDestDelta(width);
    }

    private void MovSegRegToRegMem()
    {
      SetDebug("MOV");
      var (mod, reg, rm) = ReadModRegRm();
      SetDebugSourceThenTarget(((SegmentRegister)reg).ToString());
      WriteToRegisterOrMemory(Width.Word, mod, rm, () => GetSegmentRegisterValue((SegmentRegister)reg));

      clockCount += mod == 0b11 ? 2 : 9;
    }

    private void Nop()
    {
      SetDebug("NOP");

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
      SetDebugSourceThenTarget(srcWidth == Width.Byte ? src.ToString("X2") : src.ToString("X4"));

      if (opcodes[0] == 3)
      {
        if ((src & 0x80) != 0) { src |= 0xFF00; }
      }

      switch (reg)
      {
        case 0:
        {
          SetDebug("ADD");
          var result = dstWidth == Width.Byte ? Add08((byte)dst, (byte)src) : Add16(dst, src);
          WriteToRegisterOrMemory(dstWidth, mod, rm, () => result);
          break;
        }
        case 1:
        {
          SetDebug("OR");
          var result = (ushort)(dst | src);
          SetFlagsForLogicalOp(dstWidth, result);
          WriteToRegisterOrMemory(dstWidth, mod, rm, () => result);
          break;
        }
        case 2:
        {
          SetDebug("ADC");
          var c = CarryFlag ? (byte)1 : (byte)0;
          var result = dstWidth == Width.Byte ? Add08((byte)dst, (byte)src, c) : Add16(dst, src, c);
          WriteToRegisterOrMemory(dstWidth, mod, rm, () => result);
          break;
        }
        case 3:
        {
          SetDebug("SBB");
          var c = CarryFlag ? (byte)1 : (byte)0;
          var result = dstWidth == Width.Byte ? Sub08((byte)dst, (byte)src, c) : Sub16(dst, src, c);
          WriteToRegisterOrMemory(dstWidth, mod, rm, () => result);
          break;
        }
        case 4:
        {
          SetDebug("AND");
          var result = (ushort)(dst & src);
          SetFlagsForLogicalOp(dstWidth, result);
          WriteToRegisterOrMemory(dstWidth, mod, rm, () => result);
          break;
        }
        case 5:
        {
          SetDebug("SUB");
          var result = dstWidth == Width.Byte ? Sub08((byte)dst, (byte)src) : Sub16(dst, src);
          WriteToRegisterOrMemory(dstWidth, mod, rm, () => result);
          break;
        }
        case 6:
        {
          SetDebug("XOR");
          var result = (ushort)(dst ^ src);
          SetFlagsForLogicalOp(dstWidth, result);
          WriteToRegisterOrMemory(dstWidth, mod, rm, () => result);
          break;
        }
        case 7:
        {
          SetDebug("CMP");
          if (dstWidth == Width.Byte) { Sub08((byte)dst, (byte)src); }
          else { Sub16(dst, src); }
          break;
        }
      }

      clockCount += mod == 0b11 ? 4 : 17;
    }

    private void OpcodeGroup2()
    {
      var width = OpWidth;
      var (mod, reg, rm) = ReadModRegRm();

      var singleBitOpcode = (opcodes[0] & 0x2) == 0;
      var bitCount = singleBitOpcode ? (byte)1 : CL;
      var dst = ReadFromRegisterOrMemory(width, mod, rm);

      DoBitShift(width, mod, reg, rm, dst, bitCount);

      if (singleBitOpcode)
      {
        debug[3] = "1";
        clockCount += mod == 0b11 ? 2 : 15;
      }
      else
      {
        debug[3] = nameof(CL);
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
        case 1: // undocumented
          SetDebug("TEST");
          dst = width == Width.Byte ? ReadCodeByte() : ReadCodeWord();
          SetDebugSourceThenTarget(dst.ToString("X" + (byte)width * 2));
          SetFlagsForLogicalOp(width, (ushort)(dst & src));

          clockCount += mod == 0b11 ? 5 : 11;
          break;
        case 2:
          SetDebug("NOT");
          WriteToRegisterOrMemory(width, mod, rm, () => (ushort)~src);

          clockCount += mod == 0b11 ? 3 : 16;
          break;
        case 3:
          SetDebug("NEG");
          if (width == Width.Byte)
          {
            if (src == 0x80) { OverflowFlag = true; }
            else
            {
              WriteToRegisterOrMemory(width, mod, rm, () => Sub08(0, (byte)src));
            }
          }
          else
          {
            if (src == 0x8000) { OverflowFlag = true; }
            else
            {
              WriteToRegisterOrMemory(width, mod, rm, () => Sub16(0, src));
            }
          }
          CarryFlag = src != 0;

          clockCount += mod == 0b11 ? 3 : 16;
          break;
        case 4:
          SetDebug("MUL");
          if (width == Width.Byte)
          {
            AX = (ushort)(AL * src);
            CarryFlag = OverflowFlag = AH != 0;

            clockCount += mod == 0b11 ? (77 - 70) / 2 : (83 - 76) / 2;
          }
          else
          {
            var result = (uint)(AX * src);
            AX = (ushort)result;
            DX = (ushort)(result >> 16);
            CarryFlag = OverflowFlag = DX != 0;

            clockCount += mod == 0b11 ? (133 - 118) / 2 : (139 - 124) / 2;
          }
          break;
        case 5:
          SetDebug("IMUL");

          if (width == Width.Byte)
          {
            AX = (ushort)((sbyte)AL * (sbyte)src);
            CarryFlag = OverflowFlag = AH > 0 && AH < 0xFF;

            clockCount += mod == 0b11 ? (98 - 80) / 2 : (154 - 128) / 2;
          }
          else
          {
            (DX, AX) = Imul16((short)AX, (short)src);

            clockCount += mod == 0b11 ? (104 - 86) / 2 : (160 - 134) / 2;
          }
          break;
        case 6:
          SetDebug("DIV");
          if (src == 0)
          {
            DoInt(InterruptVector.CpuDivideByZero);
            return;
          }

          if (width == Width.Byte)
          {
            dst = AX;
            var result = (ushort)(dst / src);
            if (result > byte.MaxValue) { DoInt(InterruptVector.CpuDivideByZero); }
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
            if (result > ushort.MaxValue) { DoInt(InterruptVector.CpuDivideByZero); }
            else
            {
              AX = (ushort)result;
              DX = (ushort)(longdst % src);
            }

            clockCount += mod == 0b11 ? (162 - 144) / 2 : (168 - 150) / 2;
          }
          break;
        case 7:
          SetDebug("IDIV");
          if (src == 0)
          {
            DoInt(InterruptVector.CpuDivideByZero);
            return;
          }

          if (width == Width.Byte)
          {
            var ssrc = (sbyte)src;
            var sdst = (short)AX;
            var result = sdst / ssrc;
            if (result > sbyte.MaxValue || result < sbyte.MinValue) { DoInt(InterruptVector.CpuDivideByZero); }
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
            if (result > short.MaxValue || result < short.MinValue) { DoInt(InterruptVector.CpuDivideByZero); }
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
          SetDebug("INC");
          var incResult = width == Width.Byte ? Add08((byte)src, 1) : Add16(src, 1);
          WriteToRegisterOrMemory(width, mod, rm, () => incResult);
          debug[3] = null;
          CarryFlag = oldCarry;

          clockCount += mod == 0b11 ? 3 : 15;
          break;
        case 1:
          SetDebug("DEC");
          var decResult = width == Width.Byte ? Sub08((byte)src, 1) : Sub16(src, 1);
          WriteToRegisterOrMemory(width, mod, rm, () => decResult);
          debug[3] = null;
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
          SetDebug("INC");
          WriteToRegisterOrMemory(Width.Word, mod, rm, () => Add16(src, 1));
          debug[3] = null;
          CarryFlag = oldCarry;

          clockCount += mod == 0b11 ? 3 : 15;
          break;
        case 1:
          SetDebug("DEC");
          WriteToRegisterOrMemory(Width.Word, mod, rm, () => Sub16(src, 1));
          debug[3] = null;
          CarryFlag = oldCarry;

          clockCount += mod == 0b11 ? 3 : 15;
          break;
        case 2:
          SetDebug("CALL");
          DoCall(src);

          clockCount += mod == 0b11 ? 16 : 21;
          break;
        case 3:
        {
          SetDebug("CALL");
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
          SetDebug("JMP");
          IP = src;

          clockCount += mod == 0b11 ? 11 : 18;
          break;
        case 5:
        {
          SetDebug("JMP");
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
          SetDebug("PUSH");
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
      SetDebug("OR");
      HandleLogicalOpGroup((x, y) => x | y);
    }

    private void PopFlags()
    {
      SetDebug("POPF");
      SetFlags(Pop());
    }

    private void PopRegister()
    {
      var regIndex = opcodes[0] & 0b111;
      SetDebug("POP", RegisterNames[regIndex]);
      Registers[regIndex] = Pop();

      clockCount += 8;
    }

    private void PopRegMem()
    {
      SetDebug("POP");
      var (mod, reg, rm) = ReadModRegRm();
      switch (reg)
      {
        case 0:
          SetDebug("POP");
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
      SetDebug("POP", segmentRegister.ToString());
      SetSegmentRegisterValue(segmentRegister, Pop());

      clockCount += 8;
    }

    private void PushFlags()
    {
      SetDebug("PUSHF");
      Push(GetFlags());

      clockCount += 10;
    }

    private void PushRegister()
    {
      var regIndex = opcodes[0] & 0b111;
      SetDebug("PUSH", RegisterNames[regIndex]);
      Push(Registers[regIndex]);

      clockCount += 11;
    }

    private void PushSegment()
    {
      var segmentRegister = (SegmentRegister)((opcodes[0] >> 3) & 0b11);
      SetDebug("PUSH", segmentRegister.ToString());
      Push(GetSegmentRegisterValue(segmentRegister));

      clockCount += 10;
    }

    private void RetInterSeg(ushort? stackChange = null)
    {
      SetDebug("RET", stackChange?.ToString());
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
      SetDebug("RET", stackChange?.ToString());
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
      SetDebug("SAHF");
      SetFlags(AH);

      clockCount += 4;
    }

    private void Sbb()
    {
      SetDebug("SBB");
      var borrow = CarryFlag ? (byte)1 : (byte)0;
      HandleOpGroup((x, y) => Sub08(x, y, borrow), (x, y) => Sub16(x, y, borrow));
    }

    private void Scas()
    {
      var width = OpWidth;
      var addr = (ES << 4) + DI;
      if (width == Width.Byte)
      {
        SetDebug("SCASB");
        var dst = memory.ReadByte(addr);
        Sub08(AL, dst);
      }
      else
      {
        SetDebug("SCASW");
        var dst = memory.ReadWord(addr);
        Sub16(AX, dst);
      }

      DI += GetSourceOrDestDelta(width);

      if (repeatWhileNotZero == ZeroFlag) { repeatWhileNotZero = null; }

      clockCount += 15;
    }

    private void Stos()
    {
      var width = OpWidth;
      var addr = (ES << 4) + DI;
      if (width == Width.Byte)
      {
        SetDebug("STOSB");
        memory.WriteByte(addr, AL);
      }
      else
      {
        SetDebug("STOSW");
        memory.WriteWord(addr, AX);
      }

      DI += GetSourceOrDestDelta(width);

      clockCount += repeatWhileNotZero == null ? 11 : 10;
    }

    private void Sub()
    {
      SetDebug("SUB");
      HandleOpGroup((x, y) => Sub08(x, y), (x, y) => Sub16(x, y));
    }

    private byte Sub08(byte op1, byte op2, byte borrow = 0)
    {
      var temp = (ushort)(op1 - op2 - borrow);
      var result = (byte)temp;
      CarryFlag = (temp & 0xFF00) != 0;
      OverflowFlag = ((temp ^ op1) & (op2 ^ op1) & 0x80) != 0;
      AuxiliaryCarryFlag = ((op1 ^ op2 ^ temp) & 0x10) != 0;
      SetFlagsFromValue(Width.Byte, result);
      return result;
    }

    private ushort Sub16(ushort op1, ushort op2, byte borrow = 0)
    {
      var temp = (uint)(op1 - op2 - borrow);
      var result = (ushort)temp;
      CarryFlag = (temp & 0xFFFF0000U) != 0;
      OverflowFlag = ((temp ^ op1) & (op2 ^ op1) & 0x8000U) != 0;
      AuxiliaryCarryFlag = ((op1 ^ op2 ^ temp) & 0x10U) != 0;
      SetFlagsFromValue(Width.Word, result);
      return result;
    }

    private void Test(ushort regValue, ushort immValue)
    {
      var width = OpWidth;
      SetDebug("TEST", width == Width.Byte ? "AL" : "AX", immValue.ToString("X" + (byte)width * 2));

      SetFlagsForLogicalOp(width, (ushort)(regValue & immValue));

      clockCount += 5;
    }

    private void Test()
    {
      SetDebug("TEST");
      var width = OpWidth;
      var (mod, reg, rm) = ReadModRegRm();
      var dst = ReadFromRegisterOrMemory(width, mod, rm);
      var src = GetRegisterValue(width, reg);
      SetFlagsForLogicalOp(width, (ushort)(dst & src));

      clockCount += 9;
    }

    private void Wait()
    {
      SetDebug("WAIT");

      clockCount += 3; // + 5n
    }

    private void Xchg()
    {
      SetDebug("XCHG");
      var width = OpWidth;
      var (mod, reg, rm) = ReadModRegRm();

      var src = ReadFromRegisterOrMemory(width, mod, rm);
      var dst = GetRegisterValue(width, reg);
      SetDebugSourceThenTarget(GetRegisterName(width, reg));

      SetRegisterValue(width, reg, src);
      WriteToRegisterOrMemory(width, mod, rm, () => dst);

      clockCount += 17;
    }

    private void XchgAx()
    {
      var regIndex = opcodes[0] & 0b111;
      SetDebug("XCHG", "AX", RegisterNames[regIndex]);
      var tmp = AX;
      AX = Registers[regIndex];
      Registers[regIndex] = tmp;

      clockCount += 3;
    }

    private void Xlat()
    {
      SetDebug("XLAT");
      AL = ReadDataByte(BX + AL);

      clockCount += 11;
    }

    private void Xor()
    {
      SetDebug("XOR");
      HandleLogicalOpGroup((x, y) => x ^ y);
    }
  }
}