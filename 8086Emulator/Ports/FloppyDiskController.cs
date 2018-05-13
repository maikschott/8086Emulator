using System;
using System.Collections.Generic;
using System.Linq;

namespace Masch._8086Emulator.Ports
{
  public class FloppyDiskController : IInternalDevice
  {
    public IEnumerable<int> PortNumbers => Enumerable.Range(0x3F0, 8);

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
