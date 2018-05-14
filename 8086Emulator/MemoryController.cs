using System;
using System.Collections.Generic;

namespace Masch._8086Emulator
{
  public class MemoryController
  {
    public const int MemorySize = SpecialOffset.HighMemoryArea;  // = 1MB, who would ever need more?

    private readonly Dictionary<int, Action<int, byte>> registeredBlocks;
    public byte[] Memory;

    public MemoryController()
    {
      Memory = new byte[MemorySize];
      registeredBlocks = new Dictionary<int, Action<int, byte>>();
    }

    //public ref byte this[int offset] => ref Memory[offset];

    public Memory<byte> GetMemoryBlock(int startOfs, int length)
    {
      return new Memory<byte>(Memory, startOfs, length);
    }

    public byte ReadByte(int offset)
    {
      offset &= 0xFFFFF;
      return Memory[offset];
    }

    public ushort ReadWord(int offset)
    {
      offset &= 0xFFFFF;
      return (ushort)(Memory[offset] | (Memory[offset + 1] << 8));
    }

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

    public void WriteByte(int offset, byte value)
    {
      offset &= 0xFFFFF;
      Memory[offset] = value;
      if (registeredBlocks.TryGetValue(offset >> 8, out var callback)) { callback(offset, value); }
    }

    public void WriteWord(int offset, ushort value)
    {
      offset &= 0xFFFFF;
      var lo = (byte)value;
      var hi = (byte)(value >> 8);
      Memory[offset] = lo;
      Memory[offset + 1] = hi;

      if (registeredBlocks.TryGetValue(offset >> 8, out var callback))
      {
        callback(offset, lo);
        callback(offset + 1, hi);
      }
    }
  }
}