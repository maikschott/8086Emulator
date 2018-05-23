using System.Collections.Generic;
using System.Linq;

namespace Masch.Emulator8086.InternalDevices
{
  public class SerialPort8250 : IInternalDevice
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
