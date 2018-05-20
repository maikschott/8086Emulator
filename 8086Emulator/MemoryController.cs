#define _MEMORY_CALLBACK
using System;

#if MEMORY_CALLBACK
using System.Collections.Generic;
#endif

namespace Masch._8086Emulator
{
  public class MemoryController
  {
    public const int MemorySize = SpecialOffset.HighMemoryArea; // = 1MB, who would ever need more?

#if MEMORY_CALLBACK
    private readonly Dictionary<int, Action<int, byte>> registeredBlocks = new Dictionary<int, Action<int, byte>>();
#endif
    private readonly byte[] memory;

    public MemoryController()
    {
      memory = new byte[MemorySize];
    }

    //public ref byte this[int offset] => ref Memory[offset];

    public void ReadBlock(int offset, Array dest, int length)
    {
      Array.Copy(memory, offset, dest, 0, length);
    }

    public byte ReadByte(int offset)
    {
      offset &= 0xFFFFF;
      return memory[offset];
    }

    public ushort ReadWord(int offset)
    {
      offset &= 0xFFFFF;
      return (ushort)(memory[offset] | (memory[offset + 1] << 8));
    }

#if MEMORY_CALLBACK
    public void RegisterChangeNotifier(int startSegment, int exclusiveEndSegment, Action<int, byte> callback)
    {
      if (startSegment < ushort.MinValue || startSegment > ushort.MaxValue) { throw new ArgumentOutOfRangeException(nameof(startSegment)); }
      if (exclusiveEndSegment < 0 || exclusiveEndSegment - 1 > ushort.MaxValue) { throw new ArgumentOutOfRangeException(nameof(exclusiveEndSegment)); }
      if ((startSegment & 0x000F) != 0) { throw new ArgumentException(@"Start segment must be divisible by 16, i.e. have a value of 0xXXX0", nameof(startSegment)); }
      if ((exclusiveEndSegment & 0x000F) != 0) { throw new ArgumentException(@"Exclusive end segment must be divisible by 16, i.e. have a value of 0xXXX0", nameof(startSegment)); }
      if (callback == null) { throw new ArgumentNullException(nameof(callback)); }

      for (var block = startSegment >> 4; block < exclusiveEndSegment >> 4; block++)
      {
        registeredBlocks.Add(block, callback);
      }
    }
#endif

    public void WriteBlock(int offset, Array source, int length)
    {
      Array.Copy(source, 0, memory, offset, length);
    }

    public void WriteByte(int offset, byte value)
    {
      offset &= 0xFFFFF;
      if (offset > SpecialOffset.Bios) { return; } // ROM
      memory[offset] = value;
#if MEMORY_CALLBACK
      if (registeredBlocks.TryGetValue(offset >> 8, out var callback)) { callback(offset, value); }
#endif
    }

    public void WriteWord(int offset, ushort value)
    {
      offset &= 0xFFFFF;
      if (offset > SpecialOffset.Bios) { return; } // ROM
      var lo = (byte)value;
      var hi = (byte)(value >> 8);
      memory[offset] = lo;
      memory[offset + 1] = hi;

#if MEMORY_CALLBACK
      if (registeredBlocks.TryGetValue(offset >> 8, out var callback))
      {
        callback(offset, lo);
        callback(offset + 1, hi);
      }
#endif
    }
  }
}