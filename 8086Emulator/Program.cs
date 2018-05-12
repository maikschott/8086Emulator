using System;
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
      var parameters = args.Skip(1).Select(x => x.Split(new[] { '=' }, 2)).ToDictionary(x => x[0], x => x.Length == 2 ? x[1] : null);

      if (parameters.TryGetValue("textseg", out var textSegStr) && int.TryParse(textSegStr, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var textSeg))
      {
        Graphics.TextStartOfs = textSeg;
      }

      var machine = new Machine();
      machine.LoadBootstrapper(0, File.ReadAllBytes(args[0]));
      machine.Run();
    }
  }
}
