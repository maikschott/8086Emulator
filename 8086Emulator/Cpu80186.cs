using System;

namespace Masch._8086Emulator
{
  public class Cpu80186 : Cpu8086
  {
    public Cpu80186(Machine machine)
      : base(machine)
    {
    }

    protected override Action[] RegisterOperations()
    {
      var operations = base.RegisterOperations();

      operations[0x60] = Pusha;
      operations[0x61] = Popa;
      operations[0x62] = Bound;
      operations[0x68] = PushImmediate16;
      operations[0x69] = ImulImmediate16;
      operations[0x6A] = PushImmediate08;
      operations[0x6B] = ImulImmediate08;
      operations[0x6C] = Ins;
      operations[0x6E] = Outs;
      operations[0xC0] = OpcodeGroup6; // like OpcodeGroup2 but with immediate values
      operations[0xC1] = OpcodeGroup6; // like OpcodeGroup2 but with immediate values
      operations[0xC8] = Enter;
      operations[0xC9] = Leave;

      return operations;
    }

    private void Bound()
    {
      SetDebug("BOUND");
      var (mod, reg, rm) = ReadModRegRm();
      SetDebugSourceThenTarget(RegisterNames[reg]);
      var addr = currentEffectiveAddress ?? GetEffectiveAddress(mod, rm);
      var lowerBound = ReadFromMemory(Width.Word, addr);
      var upperBound = ReadFromMemory(Width.Word, addr + 2);

      if (Registers[reg] < lowerBound || Registers[reg] > upperBound)
      {
        DoInt(InterruptVector.BoundRangeExceeded);
      }

      clockCount += 13; // 286
    }

    private void Enter()
    {
      SetDebug("ENTER");
      var allocSize = ReadCodeWord();
      var nestingLevel = ReadCodeByte();

      SetDebugSourceThenTarget(allocSize.ToString("X4"));
      SetDebugSourceThenTarget(nestingLevel.ToString("X2"));

      Push(BP);
      var frameTemp = SP;
      if (nestingLevel > 0)
      {
        for (int i = 1; i < nestingLevel; i++)
        {
          BP -= 2;
          Push(BP);
        }

        Push(frameTemp);
      }

      BP = frameTemp;
      SP -= allocSize;

      // 286
      if (nestingLevel == 0) { clockCount += 11; }
      else if (nestingLevel == 1) { clockCount += 15; }
      else { clockCount += 12 + 4 + (nestingLevel - 1); }
    }

    private void ImulImmediate08()
    {
      SetDebug("IMUL");

      var (mod, reg, rm) = ReadModRegRm();
      SetDebugSourceThenTarget(RegisterNames[reg]);
      var op1 = (short)ReadFromRegisterOrMemory(Width.Word, mod, rm);
      var op2 = (sbyte)ReadCodeByte();
      Registers[reg] = Imul16(op1, op2).lo;
      debug[3] += "," + op2.ToString("X2");

      clockCount += 21; // 286
    }

    private void ImulImmediate16()
    {
      SetDebug("IMUL");

      var (mod, reg, rm) = ReadModRegRm();
      var op1 = (short)ReadFromRegisterOrMemory(Width.Word, mod, rm);
      SetDebugSourceThenTarget(RegisterNames[reg]);
      var op2 = (short)ReadCodeWord();
      Registers[reg] = Imul16(op1, op2).lo;
      debug[3] += "," + op2.ToString("X4");

      clockCount += 21; // 286
    }

    private void Ins()
    {
      SetDebug("INS");
      throw new NotImplementedException();
    }

    private void Leave()
    {
      SetDebug("LEAVE");

      SP = BP;
      BP = Pop();

      clockCount += 5; // 286
    }

    private void OpcodeGroup6()
    {
      var width = OpWidth;
      var (mod, reg, rm) = ReadModRegRm();

      var dst = ReadFromRegisterOrMemory(width, mod, rm);
      var bitCount = ReadCodeByte();

      DoBitShift(width, mod, reg, rm, dst, bitCount);

      debug[3] = bitCount.ToString("X2");
      // 286
      clockCount += (mod == 0b11 ? 5 : 8) + bitCount;
    }

    private void Outs()
    {
      SetDebug("OUTS");
      throw new NotImplementedException();
    }

    private void Popa()
    {
      SetDebug("POPA");
      DI = Pop();
      SI = Pop();
      BP = Pop();
      Pop();
      BX = Pop();
      DX = Pop();
      CX = Pop();
      AX = Pop();

      clockCount += 19; // 286
    }

    private void Pusha()
    {
      SetDebug("PUSHA");
      var sp = SP;
      Push(AX);
      Push(CX);
      Push(DX);
      Push(BX);
      Push(sp);
      Push(BP);
      Push(SI);
      Push(DI);

      clockCount += 19; // 286
    }

    private void PushImmediate08()
    {
      SetDebug("PUSH");
      var value = ReadCodeByte();
      SetDebugSourceThenTarget(value.ToString("X2"));
      Push(value);

      clockCount += 3; // 286
    }

    private void PushImmediate16()
    {
      SetDebug("PUSH");
      var value = ReadCodeWord();
      SetDebugSourceThenTarget(value.ToString("X4"));
      Push(value);

      clockCount += 3; // 286
    }
  }
}