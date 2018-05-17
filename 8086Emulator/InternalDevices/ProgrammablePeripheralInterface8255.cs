using System.Collections.Generic;
using System.Linq;

namespace Masch._8086Emulator.InternalDevices
{
  public class ProgrammablePeripheralInterface8255 : IInternalDevice
  {
    private readonly byte[] data = new byte[4];
    //private ConsoleKeyInfo lastKey;

    public IEnumerable<int> PortNumbers => Enumerable.Range(0x60, 4);

    public ProgrammablePeripheralInterface8255()
    {
      data[0] = 0x2C;
    }

    //public ProgrammablePeripheralInterface8255(ProgrammableInterruptController8259 pic)
    //{
    //  Task.Run(() =>
    //  {
    //    while (true)
    //    {
    //      lastKey = Console.ReadKey(true);
    //      pic.Invoke(Irq.Keyboard);
    //    }
    //  });
    //}

    public byte GetByte(int port)
    {
      return data[port & 0b11];
    }

    public void SetByte(int port, byte value)
    {
      data[port & 0b11] = value;
    }
  }
}
