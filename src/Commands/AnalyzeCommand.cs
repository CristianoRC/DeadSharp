using System.Diagnostics;
using DeadSharp.Analyzer;

namespace DeadSharp.Commands;

/// <summary>
/// Handles the analyze command execution
/// </summary>
public static class AnalyzeCommand
{
    /// <summary>
    /// Executes the analysis command with the provided parameters
    /// </summary>
    /// <param name="projectPath">Path to the project or solution to analyze</param>
    /// <param name="verbose">Whether to enable verbose output</param>
    public static async Task ExecuteAsync(string projectPath, bool verbose)
    {
        try
        {
            if (verbose)
            {
                Console.WriteLine($"Analyzing project at: {projectPath}");
                Console.WriteLine("Verbose mode enabled");
            }

            await AnalyzeProjectAsync(projectPath, verbose);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"Error: {ex.Message}");
            Console.ResetColor();

            if (verbose)
                Console.Error.WriteLine(ex.StackTrace);
        }
    }

    private static async Task AnalyzeProjectAsync(string projectPath, bool verbose)
    {
        var analyzer = new CodeAnalyzer(verbose);

        Console.WriteLine($"Starting analysis of project at {projectPath}...");
        var stopwatch = Stopwatch.StartNew();

        var result = await analyzer.AnalyzeAsync(projectPath);

        stopwatch.Stop();

        if (result.Success)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Analysis completed successfully!");
            Console.ResetColor();
            Console.WriteLine();

            PrintAnalysisResults(result);
        }
        else
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Analysis failed: {result.ErrorMessage}");
            Console.ResetColor();
        }
    }

    private static void PrintAnalysisResults(AnalysisResult result)
    {
        Console.WriteLine("=== ANALYSIS SUMMARY ===");
        Console.WriteLine($"Project Path: {result.ProjectPath}");
        Console.WriteLine($"Duration: {result.Duration.TotalSeconds:F2} seconds");
        Console.WriteLine($"Projects Analyzed: {result.ProjectResults.Count}");
        Console.WriteLine($"Source Files: {result.TotalSourceFiles}");
        Console.WriteLine();

        Console.WriteLine("=== CODE METRICS ===");
        Console.WriteLine($"Total Classes: {result.TotalClasses}");
        Console.WriteLine($"Total Methods: {result.TotalMethods}");

        if (result.TotalPotentialDeadClasses > 0 || result.TotalPotentialDeadMethods > 0)
        {
            Console.WriteLine();
            Console.WriteLine("=== DEAD CODE DETECTED ===");

            if (result.TotalPotentialDeadClasses > 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(
                    $"Potentially Dead Classes: {result.TotalPotentialDeadClasses} ({result.DeadClassPercentage:F1}% of all classes)");
                Console.ResetColor();
            }

            if (result.TotalPotentialDeadMethods > 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(
                    $"Potentially Dead Methods: {result.TotalPotentialDeadMethods} ({result.DeadMethodPercentage:F1}% of all methods)");
                Console.ResetColor();
            }

            // TODO: Print details of dead code items when we have actual implementation
        }
        else
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("No dead code detected!");
            Console.ResetColor();
        }

        // Per-project breakdown
        if (result.ProjectResults.Count > 1)
        {
            Console.WriteLine();
            Console.WriteLine("=== PROJECT BREAKDOWN ===");

            foreach (var projectResult in result.ProjectResults)
            {
                Console.WriteLine($"- {Path.GetFileName(projectResult.ProjectPath)}:");
                Console.WriteLine($"  - Source Files: {projectResult.SourceFileCount}");
                Console.WriteLine($"  - Classes: {projectResult.TotalClasses}");
                Console.WriteLine($"  - Methods: {projectResult.TotalMethods}");

                if (projectResult.PotentialDeadClasses > 0 || projectResult.PotentialDeadMethods > 0)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    if (projectResult.PotentialDeadClasses > 0)
                    {
                        Console.WriteLine($"  - Potentially Dead Classes: {projectResult.PotentialDeadClasses}");
                    }

                    if (projectResult.PotentialDeadMethods > 0)
                    {
                        Console.WriteLine($"  - Potentially Dead Methods: {projectResult.PotentialDeadMethods}");
                    }

                    Console.ResetColor();
                }
            }
        }
    }
}