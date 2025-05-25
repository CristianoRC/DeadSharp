using System.CommandLine;
using DeadSharp.Commands;

namespace DeadSharp;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = CommandLineOptions.CreateRootCommand();

        rootCommand.SetHandler(async (context) =>
        {
            var projectPath = context.ParseResult.GetValueForOption(CommandLineOptions.ProjectPath)!;
            var verbose = context.ParseResult.GetValueForOption(CommandLineOptions.Verbose);
            var ignoreTests = context.ParseResult.GetValueForOption(CommandLineOptions.IgnoreTests);
            var ignoreMigrations = context.ParseResult.GetValueForOption(CommandLineOptions.IgnoreMigrations);
            var ignoreAzureFunctions = context.ParseResult.GetValueForOption(CommandLineOptions.IgnoreAzureFunctions);
            var ignoreControllers = context.ParseResult.GetValueForOption(CommandLineOptions.IgnoreControllers);
            var enhancedDiDetection = context.ParseResult.GetValueForOption(CommandLineOptions.EnhancedDiDetection);
            var enhancedDataFlow = context.ParseResult.GetValueForOption(CommandLineOptions.EnhancedDataFlow);
            var outputPath = context.ParseResult.GetValueForOption(CommandLineOptions.OutputPath);

            await AnalyzeCommand.ExecuteAsync(projectPath, verbose, ignoreTests, ignoreMigrations, 
                ignoreAzureFunctions, ignoreControllers, enhancedDiDetection, enhancedDataFlow, outputPath);
        });

        return await rootCommand.InvokeAsync(args);
    }
}