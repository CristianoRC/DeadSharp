using System.Diagnostics;
using System.Text.RegularExpressions;

namespace DeadSharp.Analyzer;

/// <summary>
/// Main class responsible for analyzing C# code to identify dead code
/// </summary>
public class CodeAnalyzer
{
    private readonly bool _verbose;
    private readonly RoslynAnalyzer _roslynAnalyzer;
    
    public CodeAnalyzer(bool verbose = false)
    {
        _verbose = verbose;
        _roslynAnalyzer = new RoslynAnalyzer(verbose);
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
        
        try
        {
            // Parse solution file to extract project references
            var solutionInfo = await SolutionParser.GetSolutionInfoAsync(solutionPath);
            
            if (_verbose)
            {
                Console.WriteLine($"Solution: {solutionInfo.SolutionName}");
                Console.WriteLine($"Projects found: {solutionInfo.ProjectCount}");
                if (!string.IsNullOrEmpty(solutionInfo.VisualStudioVersion))
                {
                    Console.WriteLine($"Visual Studio version: {solutionInfo.VisualStudioVersion}");
                }
            }
            
            // Try to use Roslyn for better analysis
            try
            {
                var roslynResults = await _roslynAnalyzer.AnalyzeSolutionAsync(solutionPath);
                result.ProjectResults.AddRange(roslynResults);
                
                if (_verbose)
                {
                    Console.WriteLine($"Successfully analyzed solution with Roslyn: {roslynResults.Count} projects");
                }
            }
            catch (Exception ex)
            {
                if (_verbose)
                {
                    Console.WriteLine($"Roslyn analysis failed, falling back to basic analysis: {ex.Message}");
                }
                
                // Fallback to basic analysis
                foreach (var projectPath in solutionInfo.ProjectPaths)
                {
                    await AnalyzeProjectFileAsync(projectPath, result);
                }
            }
        }
        catch (Exception ex)
        {
            if (_verbose)
            {
                Console.WriteLine($"Error analyzing solution: {ex.Message}");
            }
            
            // Fallback: search for .csproj files in the same directory
            var solutionDir = Path.GetDirectoryName(solutionPath);
            var projectFiles = Directory.GetFiles(solutionDir!, "*.csproj", SearchOption.AllDirectories);
            
            foreach (var projectFile in projectFiles)
            {
                await AnalyzeProjectFileAsync(projectFile, result);
            }
        }
    }
    
    private async Task AnalyzeProjectFileAsync(string projectFilePath, AnalysisResult result)
    {
        if (_verbose)
        {
            Console.WriteLine($"Analyzing project: {projectFilePath}");
        }
        
        try
        {
            // Try Roslyn analysis first
            var roslynResult = await _roslynAnalyzer.AnalyzeProjectFileAsync(projectFilePath);
            
            if (roslynResult.SourceFileCount > 0)
            {
                result.ProjectResults.Add(roslynResult);
                
                if (_verbose)
                {
                    Console.WriteLine($"Successfully analyzed project with Roslyn: {roslynResult.SourceFileCount} files");
                }
                return;
            }
        }
        catch (Exception ex)
        {
            if (_verbose)
            {
                Console.WriteLine($"Roslyn analysis failed for project, falling back to basic analysis: {ex.Message}");
            }
        }
        
        // Fallback to basic analysis
        await AnalyzeProjectFileBasicAsync(projectFilePath, result);
    }
    
    private async Task AnalyzeProjectFileBasicAsync(string projectFilePath, AnalysisResult result)
    {
        // Parse project file to extract references and analyze build output
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
            
            // Basic pattern matching for fallback analysis
            // Count classes
            var classMatches = Regex.Matches(sourceCode, @"(public|internal|private|protected)?\s+class\s+(\w+)");
            result.ClassCount = classMatches.Count;
            
            // Count methods
            var methodMatches = Regex.Matches(sourceCode, @"(public|internal|private|protected)?\s+(static\s+)?\w+\s+\w+\s*\([^)]*\)");
            result.MethodCount = methodMatches.Count;
            
            // Basic dead code detection (very simple heuristics)
            await PerformBasicDeadCodeDetection(sourceCode, result);
            
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
    
    private static async Task PerformBasicDeadCodeDetection(string sourceCode, FileAnalysisResult result)
    {
        // Very basic dead code detection using regex patterns
        // This is a fallback when Roslyn analysis is not available
        
        var lines = sourceCode.Split('\n');
        
        // Find private methods that might be unused
        var privateMethodRegex = new Regex(@"private\s+(?:static\s+)?(?:\w+\s+)?(\w+)\s*\([^)]*\)", RegexOptions.Compiled);
        var methodCalls = new HashSet<string>();
        
        // First pass: collect all method calls
        foreach (var line in lines)
        {
            var callMatches = Regex.Matches(line, @"(\w+)\s*\(");
            foreach (Match match in callMatches)
            {
                methodCalls.Add(match.Groups[1].Value);
            }
        }
        
        // Second pass: find private methods that are not called
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var match = privateMethodRegex.Match(line);
            
            if (match.Success)
            {
                var methodName = match.Groups[1].Value;
                
                // Skip special methods
                if (methodName == "Main" || methodName.StartsWith("get_") || methodName.StartsWith("set_"))
                    continue;
                
                if (!methodCalls.Contains(methodName))
                {
                    var deadCodeItem = new DeadCodeItem
                    {
                        Name = methodName,
                        Type = "Method",
                        LineNumber = i + 1,
                        ColumnNumber = match.Index + 1,
                        ConfidencePercentage = 60, // Lower confidence for basic analysis
                        Reason = "Private method with no apparent references (basic analysis)"
                    };
                    
                    result.DeadMethods.Add(deadCodeItem);
                    result.PotentialDeadMethodCount++;
                }
            }
        }
        
        // Find private classes that might be unused
        var privateClassRegex = new Regex(@"private\s+class\s+(\w+)", RegexOptions.Compiled);
        var classUsages = new HashSet<string>();
        
        // Collect class usages
        foreach (var line in lines)
        {
            var usageMatches = Regex.Matches(line, @"new\s+(\w+)\s*\(|:\s*(\w+)|<(\w+)>|(\w+)\s+\w+\s*=");
            foreach (Match match in usageMatches)
            {
                for (var g = 1; g < match.Groups.Count; g++)
                {
                    if (match.Groups[g].Success)
                    {
                        classUsages.Add(match.Groups[g].Value);
                    }
                }
            }
        }
        
        // Find unused private classes
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var match = privateClassRegex.Match(line);
            
            if (match.Success)
            {
                var className = match.Groups[1].Value;
                
                if (!classUsages.Contains(className))
                {
                    var deadCodeItem = new DeadCodeItem
                    {
                        Name = className,
                        Type = "Class",
                        LineNumber = i + 1,
                        ColumnNumber = match.Index + 1,
                        ConfidencePercentage = 50, // Lower confidence for basic analysis
                        Reason = "Private class with no apparent references (basic analysis)"
                    };
                    
                    result.DeadClasses.Add(deadCodeItem);
                    result.PotentialDeadClassCount++;
                }
            }
        }
        
        await Task.CompletedTask;
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
                line.Contains("generated by") ||
                line.Contains("<auto-generated") ||
                line.Contains("This code was generated"));
        }
        catch
        {
            return false;
        }
    }
} 