namespace Masch._8086Emulator.InternalDevices
{
  public interface I16BitInternalDevice : IInternalDevice
  {
    ushort GetWord(int port);
    void SetWord(int port, ushort value);
  }
}
