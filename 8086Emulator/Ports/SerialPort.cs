using System.Collections.Generic;
using System.Linq;

namespace Masch._8086Emulator.Ports
{
  public class SerialPort : IPort
  {
    public IEnumerable<int> PortNumbers => Enumerable.Range(0x3F8, 8);

    public byte GetByte(int port)
    {
      throw new System.NotImplementedException();
    }

    public void SetByte(int port, byte value)
    {
      throw new System.NotImplementedException();
    }
  }
}
