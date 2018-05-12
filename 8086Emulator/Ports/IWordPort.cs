namespace Masch._8086Emulator.Ports
{
  public interface IWordPort : IPort
  {
    ushort GetWord(int port);
    void SetWord(int port, ushort value);
  }
}
