using System;
using System.Collections.Generic;
using System.Linq;

namespace Masch._8086Emulator.Ports
{
  public class ParallelPort : IPort
  {
    public IEnumerable<int> PortNumbers => Enumerable.Range(0x3BC, 3);

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
