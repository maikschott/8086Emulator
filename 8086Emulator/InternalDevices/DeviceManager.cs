using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Masch.Emulator8086.InternalDevices
{
  public class DeviceManager : IEnumerable<IInternalDevice>
  {
    private readonly Dictionary<int, IInternalDevice> ports;

    public DeviceManager(IEnumerable<IInternalDevice> internalDevices)
    {
      ports = internalDevices
        .SelectMany(device => device.PortNumbers, (device, port) => (port, device))
        .ToDictionary(x => x.port, x => x.device);
    }

    public IInternalDevice? this[int index] => ports.TryGetValue(index, out var device) ? device : null;
    
    public IEnumerator<IInternalDevice> GetEnumerator() => ports.Values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
    {
      return GetEnumerator();
    }
  }
}