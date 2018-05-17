using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Masch._8086Emulator.InternalDevices;

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
      int programAddr = 0;
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
          {
            var parts = parameter.Value.Split('@');
            program = File.ReadAllBytes(parts[0]);
            if (parts.Length == 2)
            {
              int.TryParse(parts[1], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out programAddr);
            }
            break;
          }
          case "textseg":
            if (int.TryParse(parameter.Value, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var textSeg))
            {
              GraphicController.TextStartOfs = textSeg;
            }
            break;
        }
      }

      var machine = new Machine();

      if (bios != null)
      {
        machine.LoadProgram((SpecialOffset.HighMemoryArea - bios.Length) >> 4, bios);
        if (program != null && programAddr > 0)
        {
          machine.LoadProgram(programAddr, program);
        }
      }
      else if (program != null)
      {
        machine.LoadAndSetBootstrapper(0, program);
        machine.Cpu.SP = 0x100;
      }

      machine.Run();
    }
  }
}
