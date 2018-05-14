using System;
using System.Collections.Generic;
using System.Linq;

namespace Masch._8086Emulator.InternalDevices
{
  public class GraphicController : IInternalDevice
  {
    private static readonly int[] mdaPorts = Enumerable.Range(0x3B0, 13).ToArray();
    private const int HerculesConfigurationSwitchRegister = 0x3BF;
    private static readonly int[] egaPorts = Enumerable.Range(0x3C0, 16).ToArray();
    private static readonly int[] cgaPorts = Enumerable.Range(0x3D0, 12).ToArray();

    public IEnumerable<int> PortNumbers =>
      mdaPorts
      .Append(HerculesConfigurationSwitchRegister)
      .Concat(egaPorts)
      .Concat(cgaPorts);

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
