using System.Collections.Generic;
using System.Linq;

namespace Masch._8086Emulator.Ports
{
  // ReSharper disable once InconsistentNaming
  public class DMAController8237 : IPort
  {
    public IEnumerable<int> PortNumbers => Enumerable.Range(0x00, 16).Concat(Enumerable.Range(0x80, 16));

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
