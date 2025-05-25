using System.CommandLine;
using DeadSharp.Commands;

namespace DeadSharp;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = CommandLineOptions.CreateRootCommand();

        rootCommand.SetHandler(
            AnalyzeCommand.ExecuteAsync,
            CommandLineOptions.ProjectPath, 
            CommandLineOptions.Verbose,
            CommandLineOptions.IgnoreTests,
            CommandLineOptions.IgnoreMigrations,
            CommandLineOptions.IgnoreAzureFunctions,
            CommandLineOptions.IgnoreControllers,
            CommandLineOptions.EnhancedDiDetection,
            CommandLineOptions.OutputPath);

        return await rootCommand.InvokeAsync(args);
    }
}