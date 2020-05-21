using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Masch.Emulator8086.CPU;
using Masch.Emulator8086.InternalDevices;
using Microsoft.Extensions.DependencyInjection;

namespace Masch.Emulator8086
{
  internal class Program
  {
    public static async Task Main(string[] args)
    {
      AppDomain.CurrentDomain.UnhandledException += CurrentDomainOnUnhandledException;
      TaskScheduler.UnobservedTaskException += TaskSchedulerOnUnobservedTaskException;

      IServiceCollection serviceCollection = new ServiceCollection();
      ConfigureServices(serviceCollection);
      var services = serviceCollection.BuildServiceProvider();

      if (args.Length == 0)
      {
        return;
      }

      var parameters = args
        .Select(arg => arg.Split(new[] {'='}, 2))
        .ToDictionary(x => x[0], x => x.Length == 2 ? x[1] : null);

      HandleProgramArguments(parameters, out var bios, out var program, out var programAddr);

      var machine = services.GetRequiredService<Machine>();

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
        services.GetRequiredService<Cpu>().SP = 0x100;
      }

      await machine.RunAsync();
    }

    private static void ConfigureServices(IServiceCollection serviceCollection)
    {
      serviceCollection.AddSingleton<Machine>();
      serviceCollection.AddSingleton<EventToken>();
      serviceCollection.AddSingleton<MemoryController>();
      serviceCollection.AddSingleton<Cpu, Cpu8086>();
      serviceCollection.AddSingleton<DeviceManager>();
      serviceCollection.AddSingleton<CMOSRealTimeClock>();
      serviceCollection.AddSingleton<CrtController6845>();
      serviceCollection.AddSingleton<DMAController8237>();
      serviceCollection.AddSingleton<ProgrammableInterruptTimer8253>();
      serviceCollection.AddSingleton<ProgrammablePeripheralInterface8255>();
      serviceCollection.AddSingleton<ProgrammableInterruptController8259>();
      //serviceCollection.AddSingleton<ParallelPort>();
      //serviceCollection.AddSingleton<SerialPort8250>();
      //serviceCollection.AddSingleton<FloppyDiskController8272>();

      serviceCollection.AddSingleton<IInternalDevice>(sp => sp.GetRequiredService<CMOSRealTimeClock>());
      serviceCollection.AddSingleton<IInternalDevice>(sp => sp.GetRequiredService<CrtController6845>());
      serviceCollection.AddSingleton<IInternalDevice>(sp => sp.GetRequiredService<DMAController8237>());
      serviceCollection.AddSingleton<IInternalDevice>(sp => sp.GetRequiredService<ProgrammableInterruptTimer8253>());
      serviceCollection.AddSingleton<IInternalDevice>(sp => sp.GetRequiredService<ProgrammablePeripheralInterface8255>());
      serviceCollection.AddSingleton<IInternalDevice>(sp => sp.GetRequiredService<ProgrammableInterruptController8259>());
      //serviceCollection.AddSingleton<IInternalDevice>(sp => sp.GetRequiredService<ParallelPort>());
      //serviceCollection.AddSingleton<IInternalDevice>(sp => sp.GetRequiredService<SerialPort8250>());
      //serviceCollection.AddSingleton<IInternalDevice>(sp => sp.GetRequiredService<FloppyDiskController8272>());
    }

    private static void HandleProgramArguments(Dictionary<string, string?> parameters, out byte[]? bios,
      out byte[]? program, out int programAddr)
    {
      bios = null;
      program = null;
      programAddr = 0;

      // example: bios=ROMs/bios.bin program=ROMs/basic.bin@F600
      foreach (var parameter in parameters)
      {
        switch (parameter.Key)
        {
          case "bios":
            bios = File.ReadAllBytes(parameter.Value);
            break;

          case "program":
          {
            var parts = parameter.Value?.Split('@');
            if (parts == null) { continue; }

            program = File.ReadAllBytes(parts[0]);
            if (parts.Length == 2)
            {
              int.TryParse(parts[1], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out programAddr);
            }

            break;
          }
          case "textseg":
            if (int.TryParse(parameter.Value, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture,
              out var textSeg))
            {
              CrtController6845.textStartOfs = textSeg;
            }

            break;
        }
      }
    }

    private static void CurrentDomainOnUnhandledException(object sender, UnhandledExceptionEventArgs unhandledExceptionEventArgs)
    {
      Console.Error.WriteLine(unhandledExceptionEventArgs.ExceptionObject);
    }

    private static void TaskSchedulerOnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs unobservedTaskExceptionEventArgs)
    {
      Console.Error.WriteLine(unobservedTaskExceptionEventArgs.Exception);
    }
  }
}