using System;
using Masch._8086Emulator.Ports;

namespace Masch._8086Emulator
{
  public partial class Cpu
  {
    private void Aaa()
    {
      if ((AL & 0x0F) > 9 || Flags.HasFlag(CpuFlags.AuxiliaryCarry))
      {
        AL += 6;
        AH++;
        SetFlag(CpuFlags.AuxiliaryCarry | CpuFlags.Carry);
      }
      else
      {
        ClearFlag(CpuFlags.AuxiliaryCarry | CpuFlags.Carry);
      }
      AL &= 0x0F;
    }

    private void Aad()
    {
      AX = (byte)(AH * ReadCodeByte() + AL);
      SetFlagsFromValue(OpWidth.Byte, AL);
    }

    private void Aam()
    {
      var value = ReadCodeByte();
      if (value == 0)
      {
        Int(InterruptVector.DivideByZero);
      }
      else
      {
        AH = (byte)(AL / value);
        AL = (byte)(AL % value);
        SetFlagsFromValue(OpWidth.Word, AX);
      }
    }

    private void Aas()
    {
      if ((AL & 0x0F) > 9 || Flags.HasFlag(CpuFlags.AuxiliaryCarry))
      {
        AL -= 6;
        AH--;
        SetFlag(CpuFlags.AuxiliaryCarry | CpuFlags.Carry);
      }
      else
      {
        ClearFlag(CpuFlags.AuxiliaryCarry | CpuFlags.Carry);
      }
      AL &= 0x0F;
    }

    private void Adc(byte relOpcode)
    {
      var c = Flags.HasFlag(CpuFlags.Carry) ? (byte)1 : (byte)0;
      HandleOpGroup(relOpcode, (x, y) => Add08(x, y, c), (x, y) => Add16(x, y, c));
    }

    private void Add(byte relOpcode)
    {
      HandleOpGroup(relOpcode, (x, y) => Add08(x, y), (x, y) => Add16(x, y));
    }

    private void Add_Or_Adc_Sbb_And_Sub_Xor_Cmp(byte opcode)
    {
      // relOpcode = 0: XXX REG8/MEM8,IMMED8
      // relOpcode = 1: XXX REG16/MEM16,IMMED16
      // relOpcode = 2: XXX REG8/MEM8,IMMED8
      // relOpcode = 3: XXX REG16/MEM16,IMMED8

      var dstWidth = GetOpWidth(opcode);
      opcode &= 0x3;
      var srcWidth = opcode != 1 ? OpWidth.Byte : OpWidth.Word;

      var (mod, reg, rm) = ReadModRegRm();
      var dst = ReadFromRegisterOrMemory(dstWidth, mod, rm);
      var src = srcWidth == OpWidth.Word ? ReadCodeWord() : ReadCodeByte();
      DebugSourceThenTarget(srcWidth == OpWidth.Byte ? src.ToString("X2") : src.ToString("X4"));

      if (opcode == 3)
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
          var result = dstWidth == OpWidth.Byte ? Add08((byte)dst, (byte)src) : Add16(dst, src);
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
          var c = Flags.HasFlag(CpuFlags.Carry) ? (byte)1 : (byte)0;
          var result = dstWidth == OpWidth.Byte ? Add08((byte)dst, (byte)src, c) : Add16(dst, src, c);
          WriteToRegisterOrMemory(dstWidth, mod, rm, () => result);
          break;
        }
        case 3:
        {
          Debug("SBB");
          var c = Flags.HasFlag(CpuFlags.Carry) ? (byte)1 : (byte)0;
          var result = dstWidth == OpWidth.Byte ? Sub08((byte)dst, (byte)src, c) : Sub16(dst, src, c);
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
          var result = dstWidth == OpWidth.Byte ? Sub08((byte)dst, (byte)src) : Sub16(dst, src);
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
          if (dstWidth == OpWidth.Byte) { Sub08((byte)dst, (byte)src); }
          else { Sub16(dst, src); }
          break;
        }
      }
      debugParam = oldDebugParam;
    }

    private byte Add08(byte x, byte y, byte c = 0)
    {
      var temp = (ushort)(x + y + c);
      var result = (byte)temp;
      SetFlag(CpuFlags.Carry, (temp & 0xFF00) != 0);
      SetFlag(CpuFlags.Overflow, ((temp ^ x) & (temp ^ y) & 0x80) != 0);
      SetFlag(CpuFlags.AuxiliaryCarry, ((x ^ y ^ temp) & 0x10) != 0);
      SetFlagsFromValue(OpWidth.Byte, temp);
      return result;
    }

    private ushort Add16(ushort x, ushort y, byte c = 0)
    {
      var temp = (uint)(x + y + c);
      var result = (ushort)temp;
      SetFlag(CpuFlags.Carry, (temp & 0xFFFF0000U) != 0);
      SetFlag(CpuFlags.Overflow, ((temp ^ x) & (temp ^ y) & 0x8000U) != 0);
      SetFlag(CpuFlags.AuxiliaryCarry, ((x ^ y ^ temp) & 0x10U) != 0);
      SetFlagsFromValue(OpWidth.Word, result);
      return result;
    }

    private void And(byte relOpcode)
    {
      HandleLogicalOpGroup(relOpcode, (x, y) => (byte)(x & y), (x, y) => (ushort)(x & y));
    }

    private void Call(ushort offset)
    {
      Push(IP);
      IP = offset;
    }

    private void Call(ushort offset, ushort segment)
    {
      Push(CS);
      Push(IP);
      IP = offset;
      CS = segment;
    }

    private void Cbw()
    {
      AH = (AL & 0x80) == 0 ? (byte)0x00 : (byte)0xFF;
    }

    private void ClearFlag(CpuFlags flag)
    {
      Flags &= ~flag;
    }

    private void Cmp(byte relOpcode)
    {
      HandleOpGroup(relOpcode, (x, y) =>
      {
        Sub08(x, y);
        return null;
      }, (x, y) =>
      {
        Sub16(x, y);
        return null;
      });
    }

    private void Cmps(OpWidth width)
    {
      var dst = ReadFromMemory(width, ES * 16 + DI);
      var src = ReadFromMemory(width, DataSegment * 16 + SI);
      if (width == OpWidth.Byte) { Sub08((byte)src, (byte)dst); } else { Sub16(src, dst); }
      if (Flags.HasFlag(CpuFlags.Direction))
      {
        SI -= (byte)width;
        DI -= (byte)width;
      }
      else
      {
        SI += (byte)width;
        DI += (byte)width;
      }
      var zf = Flags.HasFlag(CpuFlags.Zero);
      if (repeat == Repeat.Positive && !zf || repeat == Repeat.Negative && zf)
      {
        repeat = Repeat.No;
      }
    }

    private void Cwd()
    {
      DX = (AX & 0x8000) == 0 ? (ushort)0x0000 : (ushort)0xFFFF;
    }

    private void Daa()
    {
      var oldal = AL;
      var oldcf = Flags.HasFlag(CpuFlags.Carry);
      SetFlag(CpuFlags.Carry, false);
      if ((AL & 0xF) > 9 || Flags.HasFlag(CpuFlags.AuxiliaryCarry))
      {
        AL += 6;
        SetFlag(CpuFlags.Carry, oldcf || (AL & 0x80) != 0);
        SetFlag(CpuFlags.AuxiliaryCarry, true);
      }
      else
      {
        SetFlag(CpuFlags.AuxiliaryCarry, false);
      }
      if (oldal > 0x99 || oldcf)
      {
        AL += 0x60;
        SetFlag(CpuFlags.Carry, true);
      }
      else
      {
        SetFlag(CpuFlags.Carry, false);
      }

      SetFlagsFromValue(OpWidth.Byte, AL);
    }

    private void Das()
    {
      var oldal = AL;
      var oldcf = Flags.HasFlag(CpuFlags.Carry);
      SetFlag(CpuFlags.Carry, false);
      if ((AL & 0xF) > 9 || Flags.HasFlag(CpuFlags.AuxiliaryCarry))
      {
        AL -= 6;
        SetFlag(CpuFlags.Carry, oldcf || AL > 0);
        SetFlag(CpuFlags.AuxiliaryCarry, true);
      }
      else
      {
        SetFlag(CpuFlags.AuxiliaryCarry, false);
      }
      if (oldal > 0x99 || oldcf)
      {
        AL -= 0x60;
        SetFlag(CpuFlags.Carry, true);
      }
      else
      {
        SetFlag(CpuFlags.Carry, false);
      }
      SetFlagsFromValue(OpWidth.Byte, AL);
    }

    private void DecRegister(byte regIndex)
    {
      var oldCarry = Flags.HasFlag(CpuFlags.Carry);
      Registers[regIndex] = Sub16(Registers[regIndex], 1);
      SetFlag(CpuFlags.Carry, oldCarry);
    }

    private void Esc()
    {
      var (mod, _, rm) = ReadModRegRm();
      GetEffectiveAddress(mod, rm);
    }

    private void Hlt()
    {
      machine.Running = false;
    }

    private void Inc_Dec(byte opcode)
    {
      var width = GetOpWidth(opcode);
      var (mod, reg, rm) = ReadModRegRm();
      var src = ReadFromRegisterOrMemory(width, mod, rm);
      var oldCarry = Flags.HasFlag(CpuFlags.Carry);
      switch (reg)
      {
        case 0:
          Debug("INC");
          var incResult = width == OpWidth.Byte ? Add08((byte)src, 1) : Add16(src, 1);
          WriteToRegisterOrMemory(width, mod, rm, () => incResult);
          SetFlag(CpuFlags.Carry, oldCarry);
          break;
        case 1:
          Debug("DEC");
          var decResult = width == OpWidth.Byte ? Sub08((byte)src, 1) : Sub16(src, 1);
          WriteToRegisterOrMemory(width, mod, rm, () => decResult);
          SetFlag(CpuFlags.Carry, oldCarry);
          break;
        default:
          UnknownOpcode(opcode, mod, reg, rm);
          break;
      }
    }

    private void Inc_Dec_Call_Jmp_Push()
    {
      var (mod, reg, rm) = ReadModRegRm();
      var src = ReadFromRegisterOrMemory(OpWidth.Word, mod, rm);

      var oldCarry = Flags.HasFlag(CpuFlags.Carry);
      switch (reg)
      {
        case 0:
          Debug("INC");
          WriteToRegisterOrMemory(OpWidth.Word, mod, rm, () => Add16(src, 1));
          SetFlag(CpuFlags.Carry, oldCarry);
          break;
        case 1:
          Debug("DEC");
          WriteToRegisterOrMemory(OpWidth.Word, mod, rm, () => Sub16(src, 1));
          SetFlag(CpuFlags.Carry, oldCarry);
          break;
        case 2:
          Debug("CALL");
          Call(src);
          break;
        case 3:
        {
          Debug("CALL");
          if (currentEffectiveAddress == null) // mod == 0b11
          {
            UnknownOpcode(0xFF, mod, reg, rm);
            break;
          }
          Call(memory.ReadWord(currentEffectiveAddress.Value), memory.ReadWord(currentEffectiveAddress.Value + 2));
          break;
        }
        case 4:
          Debug("JMP");
          IP = src;
          break;
        case 5:
        {
          Debug("JMP");
          if (currentEffectiveAddress == null) // mod == 0b11
          {
            UnknownOpcode(0xFF, mod, reg, rm);
            break;
          }
          Jmp(memory.ReadWord(currentEffectiveAddress.Value), memory.ReadWord(currentEffectiveAddress.Value + 2));
          break;
        }
        case 6:
          Debug("PUSH");
          Push(src);
          break;
        default:
          UnknownOpcode(0xFF, mod, reg, rm);
          break;
      }
    }

    private void IncRegister(byte regIndex)
    {
      var oldCarry = Flags.HasFlag(CpuFlags.Carry);
      Registers[regIndex] = Add16(Registers[regIndex], 1);
      SetFlag(CpuFlags.Carry, oldCarry);
    }

    private void Int(InterruptVector interrupt)
    {
      Pushf();
      ClearFlag(CpuFlags.InterruptEnable | CpuFlags.Trap);
      var tableOfs = (byte)interrupt * 4;
      var ofs = memory.ReadWord(tableOfs);
      var segment = memory.ReadWord(tableOfs + 2);
      if (ofs == 0 && segment == 0) { throw new InvalidOperationException($"No handler for interrupt {(byte)interrupt} found"); }
      Call(ofs, segment);
    }

    private void Into()
    {
      if (Flags.HasFlag(CpuFlags.Overflow))
      {
        Int(InterruptVector.Overflow);
      }
    }

    private void Iret()
    {
      RetInterSeg();
      Popf();
    }

    private void Jmp(ushort offset, ushort segment)
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
      }
    }

    private void Lahf()
    {
      AH = (byte)Flags;
    }

    private void Lds()
    {
      var (mod, reg, rm) = ReadModRegRm();
      var addr = GetEffectiveAddress(mod, rm);
      Registers[reg] = memory.ReadWord(addr);
      DS = memory.ReadWord(addr + 2);
    }

    private void Lea()
    {
      var (mod, reg, rm) = ReadModRegRm();
      var addr = GetEffectiveAddress(mod, rm, false);
      Registers[reg] = (ushort)addr;
    }

    private void Les()
    {
      var (mod, reg, rm) = ReadModRegRm();
      var addr = GetEffectiveAddress(mod, rm);
      Registers[reg] = memory.ReadWord(addr);
      ES = memory.ReadWord(addr + 2);
    }

    private void Lock()
    {
    }

    private void Lods(OpWidth width)
    {
      SetRegisterValue(width, 0 /* AL or AX */, ReadFromMemory(width, DataSegment * 16 + SI));
      if (Flags.HasFlag(CpuFlags.Direction))
      {
        SI -= (byte)width;
      }
      else
      {
        SI += (byte)width;
      }
    }

    private void Loop()
    {
      var relAddr = (sbyte)ReadCodeByte();
      Debug($"LOOP {SignedHex(relAddr)}");
      if (--CX != 0)
      {
        IP = (ushort)(IP + relAddr);
      }
    }

    private void LoopEqual()
    {
      var relAddr = (sbyte)ReadCodeByte();
      Debug($"LOOPE {SignedHex(relAddr)}");
      if (--CX != 0 && Flags.HasFlag(CpuFlags.Zero))
      {
        IP = (ushort)(IP + relAddr);
      }
    }

    private void LoopNotEqual()
    {
      var relAddr = (sbyte)ReadCodeByte();
      Debug($"LOOPNE {SignedHex(relAddr)}");
      if (--CX != 0 && !Flags.HasFlag(CpuFlags.Zero))
      {
        IP = (ushort)(IP + relAddr);
      }
    }

    private void MovMemValue(byte opcode)
    {
      var width = GetOpWidth(opcode);
      var (mod, reg, rm) = ReadModRegRm();

      switch (reg)
      {
        case 0:
          WriteToRegisterOrMemory(width, mod, rm, () => width == OpWidth.Byte ? ReadCodeByte() : ReadCodeWord());
          break;
        default:
          UnknownOpcode(opcode, mod, reg, rm);
          break;
      }
    }

    private void MovRegMem(byte opcode)
    {
      var width = GetOpWidth(opcode);
      var (mod, reg, rm) = ReadModRegRm();
      if (IsRegisterSource(opcode))
      {
        var src = GetRegisterValue(width, reg);
        DebugSourceThenTarget(GetRegisterName(width, reg));
        WriteToRegisterOrMemory(width, mod, rm, () => src);
      }
      else
      {
        var src = ReadFromRegisterOrMemory(width, mod, rm);
        DebugSourceThenTarget(GetRegisterName(width, reg));
        SetRegisterValue(width, reg, src);
      }
    }

    private void MovRegMemToSegReg()
    {
      var (mod, reg, rm) = ReadModRegRm();
      SetSegmentRegisterValue((SegmentRegister)reg, ReadFromRegisterOrMemory(OpWidth.Word, mod, rm));
      DebugSourceThenTarget(((SegmentRegister)reg).ToString());
    }

    private void Movs(OpWidth width)
    {
      var srcAddr = DataSegment * 16 + SI;
      var dstAddr = DataSegment * 16 + DI;

      if (width == OpWidth.Byte)
      {
        memory.WriteByte(dstAddr, memory.ReadByte(srcAddr));
      }
      else
      {
        memory.WriteWord(dstAddr, memory.ReadWord(srcAddr));
      }

      if (Flags.HasFlag(CpuFlags.Direction))
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
      var (mod, reg, rm) = ReadModRegRm();
      DebugSourceThenTarget(((SegmentRegister)reg).ToString());
      WriteToRegisterOrMemory(OpWidth.Word, mod, rm, () => GetSegmentRegisterValue((SegmentRegister)reg));
    }

    private void Or(byte relOpcode)
    {
      HandleOpGroup(relOpcode, (x, y) => (byte)(x | y), (x, y) => (ushort)(x | y));
    }

    private ushort Pop()
    {
      var value = memory.ReadWord(SS * 16 + SP);
      SP += 2;
      return value;
    }

    private void Popf()
    {
      Flags = (CpuFlags)Pop();
    }

    private void PopRegMem()
    {
      var (mod, reg, rm) = ReadModRegRm();
      switch (reg)
      {
        case 0:
          Debug("POP");
          WriteToRegisterOrMemory(OpWidth.Word, mod, rm, Pop);
          break;
        default:
          UnknownOpcode(0x8F, mod, reg, rm);
          break;
      }
    }

    private byte PortIn08(ushort port)
    {
      return machine.Ports.TryGetValue(port, out var handler) ? handler.GetByte(port) : (byte)0;
    }

    private ushort PortIn16(ushort port)
    {
      if (machine.Ports.TryGetValue(port, out var handler))
      {
        if (handler is IWordPort wordHandler)
        {
          return wordHandler.GetWord(port);
        }

        return (ushort)(handler.GetByte(port) | (handler.GetByte(port + 1) << 8));
      }

      return 0;
    }

    private void PortOut(ushort port, byte value)
    {
      if (machine.Ports.TryGetValue(port, out var handler))
      {
        handler.SetByte(port, value);
      }
    }

    private void PortOut(ushort port, ushort value)
    {
      if (machine.Ports.TryGetValue(port, out var handler))
      {
        if (handler is IWordPort wordHandler)
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

    private void Push(ushort value)
    {
      SP -= 2;
      memory.WriteWord(SS * 16 + SP, value);
    }

    private void Pushf()
    {
      Push((ushort)Flags);
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
        else { IP = originalIP; }
      }
    }

    private void RetInterSeg(ushort stackChange = 0)
    {
      IP = Pop();
      CS = Pop();
      SP += stackChange;
    }

    private void RetIntraSeg(ushort stackChange = 0)
    {
      IP = Pop();
      SP += stackChange;
    }

    private void Rol_Ror_Rcl_Rcr_Shl_Shr(byte opcode)
    {
      var width = GetOpWidth(opcode);
      var (mod, reg, rm) = ReadModRegRm();

      var dst = ReadFromRegisterOrMemory(width, mod, rm);
      var msb = width == OpWidth.Byte ? (ushort)0x80 : (ushort)0x8000;
      bool tmpcf;

      switch (reg)
      {
        case 0:
          Debug("ROL");
          tmpcf = (dst & msb) != 0;
          dst <<= 1;
          if (tmpcf) { dst |= 1; }
          SetFlag(CpuFlags.Carry, tmpcf);
          SetFlag(CpuFlags.Overflow, tmpcf != ((dst & msb) != 0));
          break;
        case 1:
          Debug("ROR");
          tmpcf = (dst & 1) != 0;
          dst >>= 1;
          if (tmpcf) { dst |= msb; }
          SetFlag(CpuFlags.Carry, tmpcf);
          SetFlag(CpuFlags.Overflow, tmpcf != ((dst & (msb >> 1)) != 0));
          break;
        case 2:
          Debug("RCL");
          tmpcf = Flags.HasFlag(CpuFlags.Carry);
          SetFlag(CpuFlags.Carry, (dst & msb) != 0);
          dst <<= 1;
          if (tmpcf) { dst |= 1; }
          SetFlag(CpuFlags.Overflow, Flags.HasFlag(CpuFlags.Carry) != ((dst & msb) != 0));
          break;
        case 3:
          Debug("RCR");
          tmpcf = Flags.HasFlag(CpuFlags.Carry);
          SetFlag(CpuFlags.Carry, (dst & 1) != 0);
          dst >>= 1;
          if (tmpcf) { dst |= msb; }
          SetFlag(CpuFlags.Overflow, tmpcf != ((dst & (msb >> 1)) != 0));
          break;
        case 4:
          Debug("SAL/SHL");
          tmpcf = (dst & msb) != 0;
          dst <<= 1;
          SetFlagsForLogicalOp(width, dst);
          SetFlag(CpuFlags.Carry, tmpcf);
          SetFlag(CpuFlags.Overflow, tmpcf != ((dst & msb) != 0));
          break;
        case 5:
          Debug("SHR");
          tmpcf = (dst & 1) != 0;
          dst >>= 1;
          SetFlagsForLogicalOp(width, dst);
          SetFlag(CpuFlags.Carry, tmpcf);
          SetFlag(CpuFlags.Overflow, (dst & msb) != 0 != ((dst & (msb >> 1)) != 0));
          break;
        case 7:
          Debug("SAR");
          tmpcf = (dst & 1) != 0;
          var sign = (ushort)(dst & msb);
          dst = (ushort)(((dst & ~msb) >> 1) | sign);
          SetFlagsForLogicalOp(width, dst);
          SetFlag(CpuFlags.Carry, tmpcf);
          break;
        default:
          UnknownOpcode(opcode, mod, reg, rm);
          return;
      }
      WriteToRegisterOrMemory(width, mod, rm, () => dst);
    }

    private void Sahf()
    {
      Flags = (CpuFlags)(((ushort)Flags & 0xFF00) | AH);
    }

    private void Sbb(byte relOpcode)
    {
      var c = Flags.HasFlag(CpuFlags.Carry) ? (byte)1 : (byte)0;
      HandleOpGroup(relOpcode, (x, y) => Sub08(x, y, c), (x, y) => Sub16(x, y, c));
    }

    private void Scas(OpWidth width)
    {
      var dst = ReadFromMemory(width, ES * 16 + DI);
      if (width == OpWidth.Byte) { Sub08(AL, (byte)dst); } else { Sub16(AX, dst); }
      if (Flags.HasFlag(CpuFlags.Direction)) { DI -= (byte)width; } else { DI += (byte)width; }

      var zf = Flags.HasFlag(CpuFlags.Zero);
      if (repeat == Repeat.Positive && !zf || repeat == Repeat.Negative && zf)
      {
        repeat = Repeat.No;
      }
    }

    private void Stos(OpWidth width)
    {
      WriteToMemory(width, DataSegment * 16 + DI, GetRegisterValue(width, 0 /* AL or AX */));
      if (Flags.HasFlag(CpuFlags.Direction))
      {
        SI -= (byte)width;
      }
      else
      {
        SI += (byte)width;
      }
    }

    private void Sub(byte relOpcode)
    {
      HandleOpGroup(relOpcode, (x, y) => Sub08(x, y), (x, y) => Sub16(x, y));
    }

    private byte Sub08(byte x, byte y, byte c = 0)
    {
      var temp = (ushort)(x - y - c);
      var result = (byte)temp;
      SetFlag(CpuFlags.Carry, (temp & 0xFF00) != 0);
      SetFlag(CpuFlags.Overflow, ((temp ^ x) & (temp ^ y) & 0x80) != 0);
      SetFlag(CpuFlags.AuxiliaryCarry, ((x ^ y ^ temp) & 0x10) != 0);
      SetFlagsFromValue(OpWidth.Byte, temp);
      return result;
    }

    private ushort Sub16(ushort x, ushort y, byte c = 0)
    {
      var temp = (uint)(x - y - c);
      var result = (ushort)temp;
      SetFlag(CpuFlags.Carry, (temp & 0xFFFF0000U) != 0);
      SetFlag(CpuFlags.Overflow, ((temp ^ x) & (temp ^ y) & 0x8000U) != 0);
      SetFlag(CpuFlags.AuxiliaryCarry, ((x ^ y ^ temp) & 0x10U) != 0);
      SetFlagsFromValue(OpWidth.Word, result);
      return result;
    }

    private void Test(OpWidth width, ushort value1, ushort value2)
    {
      SetFlagsForLogicalOp(width, (ushort)(value1 & value2));
    }

    private void Test(byte opcode)
    {
      var width = GetOpWidth(opcode);
      var (mod, reg, rm) = ReadModRegRm();
      var dst = ReadFromRegisterOrMemory(width, mod, rm);
      var src = GetRegisterValue(width, reg);
      SetFlagsForLogicalOp(width, (ushort)(dst & src));
    }

    private void Test_Not_Neg_Mul_Imul_Div_Idiv(byte opcode)
    {
      var width = GetOpWidth(opcode);
      var (mod, reg, rm) = ReadModRegRm();
      var src = ReadFromRegisterOrMemory(width, mod, rm);
      ushort dst;

      switch (reg)
      {
        case 0:
          Debug("TEST");
          dst = width == OpWidth.Byte ? ReadCodeByte() : ReadCodeWord();
          SetFlagsForLogicalOp(width, (ushort)(dst & src));
          break;
        case 1:
          Debug("NOT");
          WriteToRegisterOrMemory(width, mod, rm, () => (ushort)~src);
          break;
        case 2:
          Debug("NEG");
          if (width == OpWidth.Byte)
          {
            if (src == 0xFF) { SetFlag(CpuFlags.Overflow); }
            else
            {
              WriteToRegisterOrMemory(width, mod, rm, () => Sub08(0, (byte)src));
            }
          }
          else
          {
            if (src == 0xFFFF) { SetFlag(CpuFlags.Overflow); }
            else
            {
              WriteToRegisterOrMemory(width, mod, rm, () => Sub16(0, src));
            }
          }
          SetFlag(CpuFlags.Carry, src > 0);
          break;
        case 3:
          Debug("MUL");
          if (width == OpWidth.Byte)
          {
            AX = (ushort)(AL * src);
            SetFlag(CpuFlags.Carry | CpuFlags.Overflow, AH > 0);
          }
          else
          {
            var result = AX * src;
            AX = (ushort)result;
            DX = (ushort)(result >> 16);
            SetFlag(CpuFlags.Carry | CpuFlags.Overflow, DX > 0);
          }
          break;
        case 4:
          Debug("IMUL");
          if (width == OpWidth.Byte)
          {
            AX = (ushort)((sbyte)AL * (sbyte)src);
            SetFlag(CpuFlags.Carry | CpuFlags.Overflow, AH > 0 && AH < 0xFF);
          }
          else
          {
            var result = (short)AX * (short)src;
            AX = (ushort)result;
            DX = (ushort)(result >> 16);
            SetFlag(CpuFlags.Carry | CpuFlags.Overflow, DX > 0 && DX < 0xFFFF);
          }
          break;
        case 5:
          Debug("DIV");
          if (src == 0)
          {
            Int(InterruptVector.DivideByZero);
            return;
          }

          if (width == OpWidth.Byte)
          {
            dst = AX;
            var result = (ushort)(dst / src);
            if (result > byte.MaxValue) { Int(InterruptVector.DivideByZero); }
            else
            {
              AL = (byte)result;
              AH = (byte)(dst % src);
            }
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
          }
          break;
        case 6:
          Debug("IDIV");
          if (src == 0)
          {
            Int(InterruptVector.DivideByZero);
            return;
          }

          if (width == OpWidth.Byte)
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
          }
          break;
        default:
          UnknownOpcode(opcode, mod, reg, rm);
          break;
      }
    }

    private void Wait()
    {
    }

    private void Xchg(byte opcode)
    {
      var width = GetOpWidth(opcode);
      var (mod, reg, rm) = ReadModRegRm();
      var src = ReadFromRegisterOrMemory(width, mod, rm);
      var dst = GetRegisterValue(width, reg);
      DebugSourceThenTarget(GetRegisterName(width, reg));

      SetRegisterValue(width, reg, src);
      WriteToRegisterOrMemory(width, mod, rm, () => dst);
    }

    private void XchgAx(byte register)
    {
      if (register == 0) { return; }

      var tmp = AX;
      AX = Registers[register];
      Registers[register] = tmp;
    }

    private void Xlat()
    {
      AL = memory.ReadByte(DataSegment * 16 + (ushort)(BX + AL));
    }

    private void Xor(byte relOpcode)
    {
      HandleLogicalOpGroup(relOpcode, (x, y) => (byte)(x ^ y), (x, y) => (ushort)(x ^ y));
    }
  }
}