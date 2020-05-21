namespace Masch.Emulator8086.InternalDevices
{
  public interface I16BitInternalDevice : IInternalDevice
  {
    ushort GetWord(int port);
    void SetWord(int port, ushort value);
  }
}