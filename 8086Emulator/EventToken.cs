using System.Threading;

namespace Masch.Emulator8086
{
  public class EventToken
  {
    public CancellationTokenSource Halt { get; set; } = new CancellationTokenSource();
    public CancellationTokenSource ShutDown { get; set; } = new CancellationTokenSource();
  }
}