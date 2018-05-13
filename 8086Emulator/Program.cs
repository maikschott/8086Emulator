using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Masch._8086Emulator
{
  class Program
  {
    static void Main(string[] args)
    {
      if (args.Length == 0)
      {
        return;
      }
      var parameters = args.Select(x => x.Split(new[] { '=' }, 2)).ToDictionary(x => x[0], x => x.Length == 2 ? x[1] : null);

      byte[] bios = null;
      byte[] program = null;
      foreach (var parameter in parameters)
      {
        switch (parameter.Key)
        {
          case "bios":
            bios = File.ReadAllBytes(parameter.Value);
            break;
          case "debugtoconsole":
            Debug.Listeners.Add(new ConsoleTraceListener(true));
            break;
          case "program":
            program = File.ReadAllBytes(parameter.Value);
            break;
          case "textseg":
            if (int.TryParse(parameter.Value, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var textSeg))
            {
              Graphics.TextStartOfs = textSeg;
            }
            break;
        }
      }
      
      var machine = new Machine();

      if (bios != null)
      {
        machine.LoadProgram((SpecialOffset.HighMemoryArea - bios.Length) >> 4, bios);
      }
      if (program != null)
      {
        machine.LoadAndSetBootstrapper(0, program);
      }

      machine.Run();
    }
  }
}
