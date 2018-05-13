using System;
using System.Collections.Generic;
using System.Linq;

namespace Masch._8086Emulator.Ports
{
  // see https://wiki.osdev.org/PIC
  public class ProgrammableInterruptController8259 : IInternalDevice
  {
    public IEnumerable<int> PortNumbers => Enumerable.Range(0x20, 2);

    public void Invoke(Irq irq)
    {
    }

    public byte GetByte(int port)
    {
      throw new NotImplementedException();
    }

    public void SetByte(int port, byte value)
    {
      throw new NotImplementedException();
    }
  }
}
