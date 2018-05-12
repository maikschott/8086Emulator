namespace Masch._8086Emulator
{
  public class Keyboard
  {
    private readonly MemoryController memoryController;
    private const int BufferSize = 32;

    private int bufferIndex;

    public Keyboard(MemoryController memoryController)
    {
      this.memoryController = memoryController;
    }

    public void PutKey(char ch)
    {
      memoryController.WriteByte(0x41E + bufferIndex, (byte)ch);
      bufferIndex = (bufferIndex + 1) % BufferSize;
    }
  }
}