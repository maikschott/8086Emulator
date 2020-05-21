using System.Collections.Generic;
using System.Threading.Tasks;

namespace Masch.Emulator8086.InternalDevices
{
  // port mapping: http://bochs.sourceforge.net/techspec/PORTS.LST
  public interface IInternalDevice
  {
    IEnumerable<int> PortNumbers { get; }

    Task Task => Task.CompletedTask;

    byte GetByte(int port);
    void SetByte(int port, byte value);
  }
}