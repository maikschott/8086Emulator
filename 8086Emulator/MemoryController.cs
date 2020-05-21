using System;

namespace Masch.Emulator8086
{
  public class MemoryController
  {
    public const int MemorySize = SpecialOffset.HighMemoryArea; // = 1MB, who would ever need more?

    public readonly byte[] Memory;

    public MemoryController()
    {
      Memory = new byte[MemorySize];
    }

    //public ref byte this[int offset] => ref Memory[offset];

    public void ReadBlock(int offset, Array dest, int length)
    {
      Array.Copy(Memory, offset, dest, 0, length);
    }

    public byte ReadByte(int offset)
    {
      offset &= 0xFFFFF;
      return Memory[offset];
    }

    public ushort ReadWord(int offset)
    {
      offset &= 0xFFFFF;
      return (ushort) (Memory[offset] | (Memory[offset + 1] << 8));
    }


    public void WriteBlock(int offset, Array source, int length)
    {
      Array.Copy(source, 0, Memory, offset, length);
    }

    public void WriteByte(int offset, byte value)
    {
      offset &= 0xFFFFF;
      if (offset > SpecialOffset.Bios) return;
      Memory[offset] = value;
    }

    public void WriteWord(int offset, ushort value)
    {
      offset &= 0xFFFFF;
      if (offset > SpecialOffset.Bios) return;
      var lo = (byte) value;
      var hi = (byte) (value >> 8);
      Memory[offset] = lo;
      Memory[offset + 1] = hi;
    }
  }
}