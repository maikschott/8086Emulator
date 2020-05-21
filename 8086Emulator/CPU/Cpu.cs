using System;
using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Masch.Emulator8086.InternalDevices;
using Microsoft.Extensions.Logging;

namespace Masch.Emulator8086.CPU
{
  public abstract class Cpu : CpuState
  {
    public const int TimerTickMultiplier = 4;
    public const int Frequency = ProgrammableInterruptTimer8253.Frequency * TimerTickMultiplier; // 4.77 MHz
    protected readonly string?[] debug = new string[4];

    protected readonly ILogger logger;
    protected readonly MemoryController memoryController;
    protected readonly ProgrammableInterruptTimer8253 pit;
    protected readonly ProgrammableInterruptController8259 pic;
    protected readonly byte[] opcodes = new byte[6];
    private readonly Action[] operations;
    private readonly BitArray parity;
    protected int clockCount;
    private int cpuTickOfLastPitTick;
    protected int? currentEffectiveAddress;
    protected SegmentRegister? dataSegmentRegister;
    protected bool dataSegmentRegisterChanged;
    protected Action? irqReturnAction;
    protected (int loopStart, int loopEnd)? loop;
    protected byte opcodeIndex;
    protected bool? repeatWhileNotZero;

    protected Cpu(ILogger logger,
      MemoryController memoryController,
      ProgrammableInterruptTimer8253 pit,
      ProgrammableInterruptController8259 pic)
    {
      this.logger = logger;
      this.memoryController = memoryController;
      this.pic = pic;
      this.pit = pit;

      // ReSharper disable once VirtualMemberCallInConstructor
      operations = RegisterOperations();
      parity = CalcParityTable();
      Reset();
    }

    // ReSharper disable once ConvertToAutoPropertyWithPrivateSetter
    public int ClockCount => clockCount;

    protected Width OpWidth => (opcodes[0] & 0x1) == 0 ? Width.Byte : Width.Word;

    protected bool IsRegisterSource => ((opcodes[0] >> 1) & 0x1) == 0;

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

    public void Reset()
    {
      CarryFlag = ParityFlag = AuxiliaryCarryFlag = ZeroFlag = SignFlag = TrapFlag = InterruptEnableFlag = DirectionFlag = OverflowFlag = false;
      CS = SpecialOffset.BootStrapping >> 4;
      IP = 0x0000;
      DS = SS = ES = 0x0000;
      Array.Clear(Registers, 0, Registers.Length);
      dataSegmentRegister = null;
      dataSegmentRegisterChanged = false;
      repeatWhileNotZero = null;
      clockCount = 0;
      cpuTickOfLastPitTick = 0;
      loop = null;
    }

    public void Tick()
    {
      var remainingPitTicks = (clockCount - cpuTickOfLastPitTick) / TimerTickMultiplier;
      if (remainingPitTicks > 0)
      {
        for (var i = cpuTickOfLastPitTick; i < clockCount; i += TimerTickMultiplier)
        {
          pit.Tick();
          cpuTickOfLastPitTick = i;
        }
      }

#if TRACE
      debug[0] = $"[{CS:X4}:{IP:X4}] AX={AX:X4} BX={BX:X4} CX={CX:X4} DX={DX:X4} SI={SI:X4} DI={DI:X4} BP={BP:X4} SP={SP:X4} DS={DS:X4} ES={ES:X4} SS={SS:X4} {(CarryFlag ? 'C' : ' ')}{(ParityFlag ? 'P' : ' ')}{(AuxiliaryCarryFlag ? 'A' : ' ')}{(ZeroFlag ? 'Z' : ' ')}{(SignFlag ? 'S' : ' ')}{(InterruptEnableFlag ? 'I' : ' ')}{(DirectionFlag ? 'D' : ' ')}{(OverflowFlag ? 'O' : ' ')}  ";
      debug[1] = null;
      debug[2] = null;
      debug[3] = null;
#endif

      //if (IP == 0xE4DC) Debugger.Break();

      currentEffectiveAddress = null;
      memoryController.ReadBlock((CS << 4) + IP, opcodes, 6);
      opcodeIndex = 0;
      var opcode = ReadCodeByte();
      operations[opcode]();
      if (dataSegmentRegisterChanged) { dataSegmentRegisterChanged = false; }
      else { dataSegmentRegister = null; }

      PrintOpcode();

      ProcessIrqs();

      if (TrapFlag) { DoInt(InterruptVector.CpuDebug); } // has lowest interrupt priority
    }

    protected abstract void DoInt(InterruptVector interruptVector, Action? flagAction = null);

    protected int GetEffectiveAddress(byte mod, byte rm, bool useSegment = true)
    {
      string? addrText = null;
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
      var useStackSegment = false;
      var ea = 0;
      switch (rm)
      {
        case 0b000:
          SetDebugSourceThenTarget($"[BX+SI{addrText}]");
          ea = BX + SI + disp;
          clockCount += 7;
          break;
        case 0b001:
          SetDebugSourceThenTarget($"[BX+DI{addrText}]");
          ea = BX + DI + disp;
          clockCount += 8;
          break;
        case 0b010:
          SetDebugSourceThenTarget($"[BP+SI{addrText}]");
          ea = BP + SI + disp;
          useStackSegment = true;
          clockCount += 8;
          break;
        case 0b011:
          SetDebugSourceThenTarget($"[BP+DI{addrText}]");
          ea = BP + DI + disp;
          useStackSegment = true;
          clockCount += 7;
          break;
        case 0b100:
          SetDebugSourceThenTarget($"[SI{addrText}]");
          ea = SI + disp;
          clockCount += 5;
          break;
        case 0b101:
          SetDebugSourceThenTarget($"[DI{addrText}]");
          ea = DI + disp;
          clockCount += 5;
          break;
        case 0b110:
          if (mod == 0b00)
          {
            ea = ReadCodeWord();
            SetDebugSourceThenTarget($"[{ea:X4}]");
            clockCount += 2;
          }
          else
          {
            SetDebugSourceThenTarget($"[BP{addrText}]");
            ea = BP + disp;
            useStackSegment = true;
            clockCount += 5;
          }
          break;
        case 0b111:
          SetDebugSourceThenTarget($"[BX{addrText}]");
          ea = BX + disp;
          clockCount += 5;
          break;
      }
      int effectiveAddress = (ushort)ea;
      if (useSegment)
      {
        var segment = DataSegment;
        if (dataSegmentRegister == null && useStackSegment) { segment = SS; }
        effectiveAddress += segment << 4;
      }
      currentEffectiveAddress = effectiveAddress;
      return effectiveAddress;
    }

    protected string GetRegisterName(Width width, int register)
    {
      return width == Width.Byte ? RegisterNames8[register] : RegisterNames[register];
    }

    protected ushort GetRegisterValue(Width width, int index)
    {
      return width == Width.Byte ? GetRegister8(index) : Registers[index];
    }

    protected ushort GetSegmentRegisterValue(SegmentRegister segmentRegister)
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

    protected ushort GetSourceOrDestDelta(Width width)
    {
      var delta = (int)width;
      if (DirectionFlag) { delta = -delta; }
      return (ushort)delta;
    }

    protected void HandleLogicalOpGroup(Func<int, int, int> func)
    {
      HandleOpGroup(
        (x, y) => (byte)SetFlagsForLogicalOp(Width.Byte, (byte)func(x, y)),
        (x, y) => SetFlagsForLogicalOp(Width.Word, (ushort)func(x, y)));
    }

    protected byte? HandleOpGroup(Func<byte, byte, byte?> func8, Func<ushort, ushort, ushort?> func16)
    {
      var relOpcode = opcodes[0] & 0b111;
      if (relOpcode == 4) // XXX AL, IMM8
      {
        var value = ReadCodeByte();
        SetDebugSourceThenTarget($"AL,{value:X2}");

        AL = func8(AL, value) ?? AL;
        clockCount += 4;
        return null;
      }
      if (relOpcode == 5) // XXX AX, IMM16
      {
        var value = ReadCodeWord();
        SetDebugSourceThenTarget($"AX,{value:X4}");

        AX = func16(AX, value) ?? AX;
        clockCount += 4;
        return null;
      }

      var (mod, reg, rm) = ReadModRegRm();
      var width = OpWidth;

      ushort src, dst;
      if (IsRegisterSource)
      {
        dst = ReadFromRegisterOrMemory(width, mod, rm);
        src = GetRegisterValue(width, reg);
        SetDebugSourceThenTarget(GetRegisterName(width, reg));
      }
      else
      {
        dst = GetRegisterValue(width, reg);
        SetDebugSourceThenTarget(GetRegisterName(width, reg));
        src = ReadFromRegisterOrMemory(width, mod, rm);
      }

      var result = width == Width.Byte ? func8((byte)dst, (byte)src) : func16(dst, src);
      if (result == null) { return mod; }

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

      return mod;
    }

    protected byte ReadCodeByte()
    {
      IP++;
      return opcodes[opcodeIndex++];
    }

    protected ushort ReadCodeWord()
    {
      IP += 2;
      return (ushort)(opcodes[opcodeIndex++] | (opcodes[opcodeIndex++] << 8));
    }

    protected byte ReadDataByte(int addr)
    {
      return memoryController.ReadByte((DataSegment << 4) + (ushort)addr);
    }

    protected ushort ReadDataWord(int addr)
    {
      return memoryController.ReadWord((DataSegment << 4) + (ushort)addr);
    }

    protected ushort ReadFromMemory(Width width, int effectiveAddress)
    {
      return width == Width.Byte ? memoryController.ReadByte(effectiveAddress) : memoryController.ReadWord(effectiveAddress);
    }

    protected ushort ReadFromRegisterOrMemory(Width width, byte mod, byte rm)
    {
      if (mod == 0b11)
      {
        SetDebugSourceThenTarget(GetRegisterName(width, rm));
        return GetRegisterValue(width, rm);
      }
      var addr = currentEffectiveAddress ?? GetEffectiveAddress(mod, rm);
      return ReadFromMemory(width, addr);
    }

    protected (byte mod, byte reg, byte rm) ReadModRegRm()
    {
      var b = ReadCodeByte();
      return ((byte)(b >> 6), (byte)((b >> 3) & 0b111), (byte)(b & 0b111));
    }

    protected abstract Action[] RegisterOperations();

    [Conditional("TRACE")]
    [DebuggerStepThrough]
    protected void SetDebug(params string?[] text)
    {
      Array.Copy(text, 0, debug, 1, text.Length);
    }

    [Conditional("TRACE")]
    [DebuggerStepThrough]
    protected void SetDebugSourceThenTarget(string text)
    {
      if (debug[2] == null) { debug[2] = text; }
      else if (debug[3] == null) { debug[3] = text; }
    }

    protected ushort SetFlagsForLogicalOp(Width width, ushort result)
    {
      OverflowFlag = CarryFlag = false;
      SetFlagsFromValue(width, result);
      return result;
    }

    protected void SetFlagsFromValue(Width width, ushort word)
    {
      var msb = width == Width.Word ? (ushort)0x8000 : (ushort)0x80;
      ParityFlag = parity.Get((byte)word);
      ZeroFlag = word == 0;
      SignFlag = (word & msb) != 0;
    }

    protected void SetRegisterValue(Width width, int index, ushort value)
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

    protected void SetSegmentRegisterValue(SegmentRegister segmentRegister, ushort value)
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

    protected static string SignedHex(sbyte value)
    {
      return $"{(value < 0 ? '-' : '+')}{Math.Abs(value):X2}";
    }

    protected static string SignedHex(short value)
    {
      return $"{(value < 0 ? '-' : '+')}{Math.Abs(value):X4}";
    }

    protected virtual void UnknownOpcode(byte mod, byte reg, byte rm)
    {
      logger.LogWarning("{0}", $"Opcode {opcodes[0]:X2}{(mod << 6) | (reg << 3) | rm:X2} not supported");
    }

    protected void WriteDataByte(int addr, byte value)
    {
      memoryController.WriteByte((DataSegment << 4) + (ushort)addr, value);
    }

    protected void WriteDataWord(int addr, ushort value)
    {
      memoryController.WriteWord((DataSegment << 4) + (ushort)addr, value);
    }

    protected void WriteToMemory(Width width, int effectiveAddress, ushort value)
    {
      if (width == Width.Byte)
      {
        memoryController.WriteByte(effectiveAddress, (byte)value);
      }
      else
      {
        memoryController.WriteWord(effectiveAddress, value);
      }
    }

    protected void WriteToRegisterOrMemory(Width width, byte mod, byte rm, Func<ushort> value)
    {
      if (mod == 0b11)
      {
        SetDebugSourceThenTarget(GetRegisterName(width, rm));
        SetRegisterValue(width, rm, value());
      }
      else
      {
        var addr = currentEffectiveAddress ?? GetEffectiveAddress(mod, rm);
        WriteToMemory(width, addr, value());
      }
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

    [Conditional("TRACE")]
    [DebuggerStepThrough]
    private void PrintOpcode()
    {
      if (loop.HasValue)
      {
        if (IP >= loop.Value.loopStart && IP <= loop.Value.loopEnd)
        {
          //return;
        }
        loop = null;
      }
      if (repeatWhileNotZero != null && !debug[1]?.StartsWith("REP") == true)
      {
        return;
      }

      if (debug[0] is { } debug0 && !debug0.StartsWith("[F000"))
      {
        logger.LogTrace("{0}", $"{debug[0]} {debug[1]} {debug[2]}{(debug[3] != null ? "," + debug[3] : null)}");
      }
    }

    private void ProcessIrqs()
    {
      if (!InterruptEnableFlag) { return; }

      var (irq, endOfInterrupt) = pic.GetIrq();
      if (irq == null) { return; }

      var oldDataSegmentRegister = dataSegmentRegister;
      dataSegmentRegister = null;

      var oldRepeat = repeatWhileNotZero;
      repeatWhileNotZero = null;

      var oldIsLooping = loop;
      loop = null;

      DoInt(irq.Value, () => InterruptEnableFlag = TrapFlag = false);
      irqReturnAction = () =>
      {
        dataSegmentRegister = oldDataSegmentRegister;
        repeatWhileNotZero = oldRepeat;
        loop = oldIsLooping;

        endOfInterrupt?.Invoke();
      };
    }

    protected enum Width : byte
    {
      Byte = 1,
      Word = 2
    }

    [SuppressMessage("ReSharper", "InconsistentNaming")]
    protected enum SegmentRegister : byte
    {
      ES,
      CS,
      SS,
      DS
    }
  }
}