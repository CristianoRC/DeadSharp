using System.Text.RegularExpressions;

namespace DeadSharp.Analyzer;

/// <summary>
/// Main class responsible for analyzing C# code to identify dead code
/// </summary>
public class CodeAnalyzer
{
    private readonly bool _verbose;
    
    public CodeAnalyzer(bool verbose = false)
    {
        _verbose = verbose;
    }
    
    /// <summary>
    /// Analyzes a project or solution to identify dead code
    /// </summary>
    /// <param name="projectPath">Path to the project or solution file</param>
    /// <returns>Analysis results</returns>
    public async Task<AnalysisResult> AnalyzeAsync(string projectPath)
    {
        var result = new AnalysisResult
        {
            ProjectPath = projectPath,
            StartTime = DateTime.Now
        };
        
        try
        {
            if (_verbose)
            {
                Console.WriteLine($"Starting analysis of {projectPath}");
            }
            
            // 1. Identify if it's a solution or project file
            var isSolution = projectPath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase);
            var isProject = projectPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase);
            var isDirectory = Directory.Exists(projectPath);
            
            if (isDirectory)
            {
                // Find all .csproj and .sln files in the directory
                var slnFiles = Directory.GetFiles(projectPath, "*.sln", SearchOption.TopDirectoryOnly);
                var projectFiles = Directory.GetFiles(projectPath, "*.csproj", SearchOption.AllDirectories);
                
                if (slnFiles.Length > 0)
                {
                    // Prefer analyzing solution file if available
                    await AnalyzeSolutionAsync(slnFiles[0], result);
                }
                else if (projectFiles.Length > 0)
                {
                    foreach (var projectFile in projectFiles)
                    {
                        await AnalyzeProjectFileAsync(projectFile, result);
                    }
                }
                else
                {
                    throw new InvalidOperationException("No .sln or .csproj files found in the specified directory.");
                }
            }
            else if (isSolution)
            {
                await AnalyzeSolutionAsync(projectPath, result);
            }
            else if (isProject)
            {
                await AnalyzeProjectFileAsync(projectPath, result);
            }
            else
            {
                throw new InvalidOperationException("The specified path is not a valid solution, project file, or directory.");
            }
            
            result.EndTime = DateTime.Now;
            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.EndTime = DateTime.Now;
            
            if (_verbose)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine($"Analysis failed: {ex.Message}");
                Console.Error.WriteLine(ex.StackTrace);
                Console.ResetColor();
            }
        }
        
        return result;
    }
    
    private async Task AnalyzeSolutionAsync(string solutionPath, AnalysisResult result)
    {
        if (_verbose)
        {
            Console.WriteLine($"Analyzing solution: {solutionPath}");
        }
        
        // TODO: Parse solution file to extract project references
        // For now, we'll just search for .csproj files in the same directory
        var solutionDir = Path.GetDirectoryName(solutionPath);
        var projectFiles = Directory.GetFiles(solutionDir!, "*.csproj", SearchOption.AllDirectories);
        
        foreach (var projectFile in projectFiles)
        {
            await AnalyzeProjectFileAsync(projectFile, result);
        }
    }
    
    private async Task AnalyzeProjectFileAsync(string projectFilePath, AnalysisResult result)
    {
        if (_verbose)
        {
            Console.WriteLine($"Analyzing project: {projectFilePath}");
        }
        
        // TODO: Parse project file to extract references and analyze build output
        // For now, we'll just collect and analyze C# files
        var projectDir = Path.GetDirectoryName(projectFilePath);
        var sourceFiles = Directory.GetFiles(projectDir!, "*.cs", SearchOption.AllDirectories);
        
        var projectResult = new ProjectAnalysisResult
        {
            ProjectPath = projectFilePath,
            SourceFileCount = sourceFiles.Length
        };
        
        foreach (var sourceFile in sourceFiles)
        {
            // Skip generated files
            if (IsGeneratedFile(sourceFile))
            {
                if (_verbose)
                {
                    Console.WriteLine($"Skipping generated file: {sourceFile}");
                }
                continue;
            }
            
            var fileResult = await AnalyzeSourceFileAsync(sourceFile);
            projectResult.FileResults.Add(fileResult);
            
            // Aggregate statistics
            projectResult.TotalMethods += fileResult.MethodCount;
            projectResult.PotentialDeadMethods += fileResult.PotentialDeadMethodCount;
            projectResult.TotalClasses += fileResult.ClassCount;
            projectResult.PotentialDeadClasses += fileResult.PotentialDeadClassCount;
        }
        
        result.ProjectResults.Add(projectResult);
    }
    
    private async Task<FileAnalysisResult> AnalyzeSourceFileAsync(string sourceFilePath)
    {
        var result = new FileAnalysisResult
        {
            FilePath = sourceFilePath,
            RelativePath = Path.GetRelativePath(Directory.GetCurrentDirectory(), sourceFilePath)
        };
        
        try
        {
            var sourceCode = await File.ReadAllTextAsync(sourceFilePath);
            
            // TODO: Implement more sophisticated code analysis using Roslyn
            // For now, just do basic pattern matching
            
            // Count classes
            var classMatches = Regex.Matches(sourceCode, @"(public|internal|private|protected)?\s+class\s+(\w+)");
            result.ClassCount = classMatches.Count;
            
            // Count methods
            var methodMatches = Regex.Matches(sourceCode, @"(public|internal|private|protected)?\s+(static\s+)?\w+\s+\w+\s*\([^)]*\)");
            result.MethodCount = methodMatches.Count;
            
            // TODO: Identify potentially dead methods and classes
            // This requires more sophisticated analysis with Roslyn
            
            if (_verbose)
            {
                Console.WriteLine($"Analyzed {result.RelativePath}: {result.ClassCount} classes, {result.MethodCount} methods");
            }
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
            if (_verbose)
            {
                Console.WriteLine($"Error analyzing {sourceFilePath}: {ex.Message}");
            }
        }
        
        return result;
    }
    
    private static bool IsGeneratedFile(string filePath)
    {
        // Check if file is in obj/ or bin/ directory
        if (filePath.Contains(Path.Combine("obj", "")) || filePath.Contains(Path.Combine("bin", "")))
        {
            return true;
        }
        
        // Check file content for auto-generated markers
        try
        {
            var firstFewLines = File.ReadLines(filePath).Take(10).ToList();
            return firstFewLines.Any(line => 
                line.Contains("auto-generated") || 
                line.Contains("autogenerated") || 
                line.Contains("generated by"));
        }
        catch
        {
            return false;
        }
    }
} 