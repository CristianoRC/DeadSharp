using System.Diagnostics;
using System.Text.Json;
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
    /// <param name="ignoreTests">Whether to ignore test projects during analysis</param>
    /// <param name="ignoreMigrations">Whether to ignore database migration files during analysis</param>
    /// <param name="ignoreAzureFunctions">Whether to ignore Azure Function files during analysis</param>
    /// <param name="ignoreControllers">Whether to ignore Controller files during analysis</param>
    /// <param name="enhancedDiDetection">Whether to enable enhanced dependency injection detection</param>
    /// <param name="outputPath">Optional path to save the analysis results in JSON format</param>
    public static async Task ExecuteAsync(string projectPath, bool verbose, bool ignoreTests = false, 
        bool ignoreMigrations = false, bool ignoreAzureFunctions = false, bool ignoreControllers = false,
        bool enhancedDiDetection = false, string? outputPath = null)
    {
        try
        {
            if (verbose)
            {
                Console.WriteLine($"Analyzing project at: {projectPath}");
                Console.WriteLine("Verbose mode enabled");
                if (ignoreTests)
                {
                    Console.WriteLine("Ignoring test projects");
                }
                if (ignoreMigrations)
                {
                    Console.WriteLine("Ignoring database migrations");
                }
                if (ignoreAzureFunctions)
                {
                    Console.WriteLine("Ignoring Azure Functions");
                }
                if (ignoreControllers)
                {
                    Console.WriteLine("Ignoring Controllers");
                }
                if (enhancedDiDetection)
                {
                    Console.WriteLine("Enhanced dependency injection detection enabled");
                }
                if (!string.IsNullOrEmpty(outputPath))
                {
                    Console.WriteLine($"Results will be saved to: {outputPath}");
                }
            }

            await AnalyzeProjectAsync(projectPath, verbose, ignoreTests, ignoreMigrations, ignoreAzureFunctions, ignoreControllers, enhancedDiDetection, outputPath);
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

    private static async Task AnalyzeProjectAsync(string projectPath, bool verbose, bool ignoreTests, 
        bool ignoreMigrations, bool ignoreAzureFunctions, bool ignoreControllers, bool enhancedDiDetection,
        string? outputPath)
    {
        var analyzer = new CodeAnalyzer(verbose, ignoreTests, ignoreMigrations, ignoreAzureFunctions, ignoreControllers, enhancedDiDetection);

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

            if (!string.IsNullOrEmpty(outputPath))
            {
                try
                {
                    var jsonOptions = new JsonSerializerOptions
                    {
                        WriteIndented = true
                    };
                    
                    var json = JsonSerializer.Serialize(result, jsonOptions);
                    await File.WriteAllTextAsync(outputPath, json);
                    
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"Results saved to: {outputPath}");
                    Console.ResetColor();
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Error.WriteLine($"Error saving results to {outputPath}: {ex.Message}");
                    Console.ResetColor();
                }
            }
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

            // Print details of dead code items
            PrintDeadCodeDetails(result);
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

    private static void PrintDeadCodeDetails(AnalysisResult result)
    {
        var allDeadItems = new List<(string FilePath, DeadCodeItem Item)>();

        // Collect all dead code items from all projects and files
        foreach (var projectResult in result.ProjectResults)
        {
            foreach (var fileResult in projectResult.FileResults)
            {
                foreach (var deadMethod in fileResult.DeadMethods)
                {
                    allDeadItems.Add((fileResult.RelativePath, deadMethod));
                }

                foreach (var deadClass in fileResult.DeadClasses)
                {
                    allDeadItems.Add((fileResult.RelativePath, deadClass));
                }
            }
        }

        if (allDeadItems.Count == 0)
            return;

        Console.WriteLine();
        Console.WriteLine("=== DEAD CODE DETAILS ===");

        // Group by confidence level for better presentation
        var highConfidence = allDeadItems.Where(x => x.Item.ConfidencePercentage >= 80).ToList();
        var mediumConfidence = allDeadItems.Where(x => x.Item.ConfidencePercentage >= 60 && x.Item.ConfidencePercentage < 80).ToList();
        var lowConfidence = allDeadItems.Where(x => x.Item.ConfidencePercentage < 60).ToList();

        if (highConfidence.Count > 0)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"HIGH CONFIDENCE ({highConfidence.Count} items):");
            Console.ResetColor();
            PrintDeadCodeItemGroup(highConfidence);
        }

        if (mediumConfidence.Count > 0)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"MEDIUM CONFIDENCE ({mediumConfidence.Count} items):");
            Console.ResetColor();
            PrintDeadCodeItemGroup(mediumConfidence);
        }

        if (lowConfidence.Count > 0)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"LOW CONFIDENCE ({lowConfidence.Count} items):");
            Console.ResetColor();
            PrintDeadCodeItemGroup(lowConfidence);
        }

        Console.WriteLine();
        Console.WriteLine("💡 Tip: Review high confidence items first. Low confidence items may be false positives.");
    }

    private static void PrintDeadCodeItemGroup(List<(string FilePath, DeadCodeItem Item)> items)
    {
        // Group by file for better organization
        var groupedByFile = items.GroupBy(x => x.FilePath).OrderBy(g => g.Key);

        foreach (var fileGroup in groupedByFile)
        {
            Console.WriteLine($"  📁 {fileGroup.Key}");

            foreach (var (_, item) in fileGroup.OrderBy(x => x.Item.LineNumber))
            {
                var icon = item.Type switch
                {
                    "Method" => "🔧",
                    "Class" => "📦",
                    "Property" => "🏷️",
                    "Field" => "📋",
                    _ => "❓"
                };

                Console.WriteLine($"    {icon} {item.Type}: {item.Name}");
                Console.WriteLine($"       📍 Line {item.LineNumber}, Column {item.ColumnNumber}");
                Console.WriteLine($"       🎯 Confidence: {item.ConfidencePercentage}%");
                Console.WriteLine($"       💭 {item.Reason}");
                Console.WriteLine();
            }
        }
    }
}