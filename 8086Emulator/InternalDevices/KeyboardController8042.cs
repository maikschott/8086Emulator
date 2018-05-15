using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Masch._8086Emulator.InternalDevices
{
  public class KeyboardController8042 : IInternalDevice
  {
    private ConsoleKeyInfo lastKey;

    public IEnumerable<int> PortNumbers => Enumerable.Range(0x60, 4);

    public KeyboardController8042(ProgrammableInterruptController8259 pic)
    {
      //Task.Run(() =>
      //{
      //  while (true)
      //  {
      //    lastKey = Console.ReadKey(true);
      //    pic.Invoke(Irq.Keyboard);
      //  }
      //});
    }

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
