using System.Collections.Generic;

namespace Masch._8086Emulator.InternalDevices
{
  // port mapping: http://bochs.sourceforge.net/techspec/PORTS.LST
  public interface IInternalDevice
  {
    IEnumerable<int> PortNumbers { get; }

    byte GetByte(int port);
    void SetByte(int port, byte value);
  }
}
