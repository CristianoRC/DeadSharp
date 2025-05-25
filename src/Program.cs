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
            CommandLineOptions.ProjectPathOption, 
            CommandLineOptions.VerboseOption);

        return await rootCommand.InvokeAsync(args);
    }
}