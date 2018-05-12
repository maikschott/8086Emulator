using System;
using System.Collections.Generic;
using System.Linq;

namespace Masch._8086Emulator.Ports
{
  public class ProgrammableInterruptController8259 : IPort
  {
    public IEnumerable<int> PortNumbers => Enumerable.Range(0x20, 2);

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
