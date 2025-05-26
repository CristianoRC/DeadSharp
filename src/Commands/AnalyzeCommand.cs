using System.Diagnostics;
using System.Text.Json;
using System.Text;
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
    /// <param name="enhancedDataFlow">Whether to enable enhanced data flow analysis</param>
    /// <param name="outputPath">Optional path to save the analysis results</param>
    /// <param name="outputFormat">Format of the output (JSON or TXT)</param>
    public static async Task ExecuteAsync(string projectPath, bool verbose, bool ignoreTests = false, 
        bool ignoreMigrations = false, bool ignoreAzureFunctions = false, bool ignoreControllers = false,
        bool enhancedDiDetection = false, bool enhancedDataFlow = false, string? outputPath = null,
        string? outputFormat = "console")
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
                if (enhancedDataFlow)
                {
                    Console.WriteLine("Enhanced data flow analysis enabled (advanced semantic analysis)");
                }
                if (!string.IsNullOrEmpty(outputPath))
                {
                    Console.WriteLine($"Results will be saved to: {outputPath}");
                    Console.WriteLine($"Output format: {outputFormat?.ToUpperInvariant() ?? "CONSOLE"}");
                }
            }

            await AnalyzeProjectAsync(projectPath, verbose, ignoreTests, ignoreMigrations, ignoreAzureFunctions, 
                ignoreControllers, enhancedDiDetection, enhancedDataFlow, outputPath, outputFormat);
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
        bool enhancedDataFlow, string? outputPath, string? outputFormat)
    {
        var analyzer = new CodeAnalyzer(verbose, ignoreTests, ignoreMigrations, ignoreAzureFunctions, ignoreControllers, enhancedDiDetection, enhancedDataFlow);

        Console.WriteLine($"Starting analysis of project at {projectPath}...");
        var stopwatch = Stopwatch.StartNew();

        var result = await analyzer.AnalyzeAsync(projectPath);

        stopwatch.Stop();

        if (result.Success)
        {
            // Se não estiver salvando em arquivo ou se o formato for console, exibe no console
            if (string.IsNullOrEmpty(outputPath) || string.Equals(outputFormat, "console", StringComparison.OrdinalIgnoreCase))
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
                // Caso contrário, salva no arquivo com o formato especificado
                try
                {
                    string formattedOutput;
                    outputFormat = outputFormat?.ToUpperInvariant() ?? "JSON";
                    
                    if (outputFormat == "JSON")
                    {
                        var jsonOptions = new JsonSerializerOptions
                        {
                            WriteIndented = true
                        };
                        
                        formattedOutput = JsonSerializer.Serialize(result, jsonOptions);
                    }
                    else // TXT
                    {
                        formattedOutput = GenerateTextReport(result);
                    }
                    
                    await File.WriteAllTextAsync(outputPath, formattedOutput);
                    
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"Analysis completed successfully!");
                    Console.WriteLine($"Results saved to: {outputPath} (Format: {outputFormat})");
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

        // Verificar se estamos executando no Windows
        bool isWindows = OperatingSystem.IsWindows();
        
        Console.WriteLine();
        Console.WriteLine($"{(isWindows ? "Tip" : "💡 Tip")}: Review high confidence items first. Low confidence items may be false positives.");
    }

    private static void PrintDeadCodeItemGroup(List<(string FilePath, DeadCodeItem Item)> items)
    {
        // Group by file for better organization
        var groupedByFile = items.GroupBy(x => x.FilePath).OrderBy(g => g.Key);
        
        // Verificar se estamos executando no Windows
        bool isWindows = OperatingSystem.IsWindows();

        foreach (var fileGroup in groupedByFile)
        {
            Console.WriteLine($"  {(isWindows ? "[DIR]" : "📁")} {fileGroup.Key}");

            foreach (var (_, item) in fileGroup.OrderBy(x => x.Item.LineNumber))
            {
                string icon = item.Type switch
                {
                    "Method" => isWindows ? "[M]" : "🔧",
                    "Class" => isWindows ? "[C]" : "📦",
                    "Property" => isWindows ? "[P]" : "🏷️",
                    "Field" => isWindows ? "[F]" : "📋",
                    _ => isWindows ? "[?]" : "❓"
                };

                Console.WriteLine($"    {icon} {item.Type}: {item.Name}");
                Console.WriteLine($"       {(isWindows ? "Line" : "📍 Line")} {item.LineNumber}, Column {item.ColumnNumber}");
                Console.WriteLine($"       {(isWindows ? "Confidence" : "🎯 Confidence")}: {item.ConfidencePercentage}%");
                Console.WriteLine($"       {(isWindows ? "Reason" : "💭")} {item.Reason}");
                Console.WriteLine();
            }
        }
    }

    // Gera relatório em formato texto
    private static string GenerateTextReport(AnalysisResult result)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("=== DEADSHARP ANALYSIS ===");
        sb.AppendLine($"Analysis Date: {DateTime.Now}");
        sb.AppendLine($"Project Path: {result.ProjectPath}");
        sb.AppendLine($"Duration: {result.Duration.TotalSeconds:F2} seconds");
        sb.AppendLine($"Projects Analyzed: {result.ProjectResults.Count}");
        sb.AppendLine($"Source Files: {result.TotalSourceFiles}");
        sb.AppendLine();

        sb.AppendLine("=== CODE METRICS ===");
        sb.AppendLine($"Total Classes: {result.TotalClasses}");
        sb.AppendLine($"Total Methods: {result.TotalMethods}");
        sb.AppendLine();

        if (result.TotalPotentialDeadClasses > 0 || result.TotalPotentialDeadMethods > 0)
        {
            sb.AppendLine("=== DEAD CODE DETECTED ===");

            if (result.TotalPotentialDeadClasses > 0)
            {
                sb.AppendLine($"Potentially Dead Classes: {result.TotalPotentialDeadClasses} ({result.DeadClassPercentage:F1}% of all classes)");
            }

            if (result.TotalPotentialDeadMethods > 0)
            {
                sb.AppendLine($"Potentially Dead Methods: {result.TotalPotentialDeadMethods} ({result.DeadMethodPercentage:F1}% of all methods)");
            }

            // Detalhes dos itens de código morto
            AppendDeadCodeDetails(sb, result);
        }
        else
        {
            sb.AppendLine("No dead code detected!");
        }

        // Detalhamento por projeto
        if (result.ProjectResults.Count > 1)
        {
            sb.AppendLine();
            sb.AppendLine("=== PROJECT DETAILS ===");

            foreach (var projectResult in result.ProjectResults)
            {
                sb.AppendLine($"- {Path.GetFileName(projectResult.ProjectPath)}:");
                sb.AppendLine($"  - Source Files: {projectResult.SourceFileCount}");
                sb.AppendLine($"  - Classes: {projectResult.TotalClasses}");
                sb.AppendLine($"  - Methods: {projectResult.TotalMethods}");

                if (projectResult.PotentialDeadClasses > 0 || projectResult.PotentialDeadMethods > 0)
                {
                    if (projectResult.PotentialDeadClasses > 0)
                    {
                        sb.AppendLine($"  - Potentially Dead Classes: {projectResult.PotentialDeadClasses}");
                    }

                    if (projectResult.PotentialDeadMethods > 0)
                    {
                        sb.AppendLine($"  - Potentially Dead Methods: {projectResult.PotentialDeadMethods}");
                    }
                }
            }
        }
        
        return sb.ToString();
    }
    
    private static void AppendDeadCodeDetails(StringBuilder sb, AnalysisResult result)
    {
        var allDeadItems = new List<(string FilePath, DeadCodeItem Item)>();

        // Coletar todos os itens de código morto de todos os projetos e arquivos
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

        sb.AppendLine();
        sb.AppendLine("=== DEAD CODE DETAILS ===");

        // Agrupar por nível de confiança para melhor apresentação
        var highConfidence = allDeadItems.Where(x => x.Item.ConfidencePercentage >= 80).ToList();
        var mediumConfidence = allDeadItems.Where(x => x.Item.ConfidencePercentage >= 60 && x.Item.ConfidencePercentage < 80).ToList();
        var lowConfidence = allDeadItems.Where(x => x.Item.ConfidencePercentage < 60).ToList();

        if (highConfidence.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"HIGH CONFIDENCE ({highConfidence.Count} items):");
            AppendDeadCodeItemGroup(sb, highConfidence);
        }

        if (mediumConfidence.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"MEDIUM CONFIDENCE ({mediumConfidence.Count} items):");
            AppendDeadCodeItemGroup(sb, mediumConfidence);
        }

        if (lowConfidence.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"LOW CONFIDENCE ({lowConfidence.Count} items):");
            AppendDeadCodeItemGroup(sb, lowConfidence);
        }
        
        sb.AppendLine();
        sb.AppendLine("Tip: Review high confidence items first. Low confidence items may be false positives.");
    }
    
    private static void AppendDeadCodeItemGroup(StringBuilder sb, List<(string FilePath, DeadCodeItem Item)> items)
    {
        // Agrupar por arquivo para melhor organização
        var groupedByFile = items.GroupBy(x => x.FilePath).OrderBy(g => g.Key);

        foreach (var fileGroup in groupedByFile)
        {
            sb.AppendLine($"  [DIR] {fileGroup.Key}");

            foreach (var (_, item) in fileGroup.OrderBy(x => x.Item.LineNumber))
            {
                string typeLabel = item.Type switch
                {
                    "Method" => "Method",
                    "Class" => "Class",
                    "Property" => "Property",
                    "Field" => "Field",
                    _ => "Unknown"
                };

                sb.AppendLine($"    [{item.Type[0]}] {typeLabel}: {item.Name}");
                sb.AppendLine($"       Line {item.LineNumber}, Column {item.ColumnNumber}");
                sb.AppendLine($"       Confidence: {item.ConfidencePercentage}%");
                sb.AppendLine($"       Reason: {item.Reason}");
                sb.AppendLine();
            }
        }
    }
}