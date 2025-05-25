using System.Diagnostics;
using System.Text.RegularExpressions;

namespace DeadSharp.Analyzer;

/// <summary>
/// Main class responsible for analyzing C# code to identify dead code
/// </summary>
public class CodeAnalyzer
{
    private readonly bool _verbose;
    private readonly bool _ignoreTests;
    private readonly bool _ignoreMigrations;
    private readonly bool _ignoreAzureFunctions;
    private readonly bool _ignoreControllers;
    private readonly bool _enhancedDiDetection;
    private readonly RoslynAnalyzer _roslynAnalyzer;
    
    public CodeAnalyzer(bool verbose = false, bool ignoreTests = false, bool ignoreMigrations = false, 
        bool ignoreAzureFunctions = false, bool ignoreControllers = false, bool enhancedDiDetection = false)
    {
        _verbose = verbose;
        _ignoreTests = ignoreTests;
        _ignoreMigrations = ignoreMigrations;
        _ignoreAzureFunctions = ignoreAzureFunctions;
        _ignoreControllers = ignoreControllers;
        _enhancedDiDetection = enhancedDiDetection;
        _roslynAnalyzer = new RoslynAnalyzer(verbose, ignoreTests, ignoreMigrations, ignoreAzureFunctions, ignoreControllers, enhancedDiDetection);
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
                
                // Check if Roslyn actually found and analyzed projects
                if (roslynResults.Count > 0 && roslynResults.Any(r => r.SourceFileCount > 0))
                {
                    result.ProjectResults.AddRange(roslynResults);
                    
                    if (_verbose)
                    {
                        Console.WriteLine($"Successfully analyzed solution with Roslyn: {roslynResults.Count} projects");
                    }
                }
                else
                {
                    if (_verbose)
                    {
                        Console.WriteLine($"Roslyn analysis returned no results, falling back to cross-project analysis");
                    }
                    
                    // Fallback to cross-project analysis
                    await PerformCrossProjectAnalysis(solutionInfo.ProjectPaths, result);
                }
            }
            catch (Exception ex)
            {
                if (_verbose)
                {
                    Console.WriteLine($"Roslyn analysis failed, falling back to cross-project analysis: {ex.Message}");
                }
                
                // Fallback to cross-project analysis
                await PerformCrossProjectAnalysis(solutionInfo.ProjectPaths, result);
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
            
            await PerformCrossProjectAnalysis(projectFiles.ToList(), result);
        }
    }
    
    private async Task AnalyzeProjectFileAsync(string projectFilePath, AnalysisResult result)
    {
        if (_verbose)
        {
            Console.WriteLine($"Analyzing project: {projectFilePath}");
        }
        
        // Filter out test projects if requested
        if (_ignoreTests && IsTestProject(projectFilePath))
        {
            if (_verbose)
            {
                Console.WriteLine($"  Detected test project by name/content: {Path.GetFileNameWithoutExtension(projectFilePath)}");
            }
            return;
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
        
        // First pass: collect all source code to analyze across files
        var allSourceCode = new Dictionary<string, string>();
        var allFileResults = new List<FileAnalysisResult>();
        
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
            
            // Skip migration files if requested
            if (_ignoreMigrations && IsMigrationFile(sourceFile))
            {
                if (_verbose)
                {
                    Console.WriteLine($"Skipping migration file: {sourceFile}");
                }
                continue;
            }
            
            // Skip Azure Function files if requested
            if (_ignoreAzureFunctions && IsAzureFunctionFile(sourceFile))
            {
                if (_verbose)
                {
                    Console.WriteLine($"Skipping Azure Function file: {sourceFile}");
                }
                continue;
            }
            
            // Skip Controller files if requested
            if (_ignoreControllers && IsControllerFile(sourceFile))
            {
                if (_verbose)
                {
                    Console.WriteLine($"Skipping Controller file: {sourceFile}");
                }
                continue;
            }
            
            try
            {
                var sourceCode = await File.ReadAllTextAsync(sourceFile);
                allSourceCode[sourceFile] = sourceCode;
                
                var fileResult = await AnalyzeSourceFileAsync(sourceFile);
                allFileResults.Add(fileResult);
                projectResult.FileResults.Add(fileResult);
                
                // Aggregate statistics
                projectResult.TotalMethods += fileResult.MethodCount;
                projectResult.TotalClasses += fileResult.ClassCount;
            }
            catch (Exception ex)
            {
                if (_verbose)
                {
                    Console.WriteLine($"Error reading file {sourceFile}: {ex.Message}");
                }
            }
        }
        
        // Second pass: perform cross-file dead code analysis
        await PerformCrossFileDeadCodeAnalysis(allSourceCode, allFileResults, projectResult);
        
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
            
            // Note: Dead code detection is now performed at the project level in PerformCrossFileDeadCodeAnalysis
            
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
    
    private async Task PerformBasicDeadCodeDetection(string sourceCode, FileAnalysisResult result)
    {
        // Very basic dead code detection using regex patterns
        // This is a fallback when Roslyn analysis is not available
        
        var lines = sourceCode.Split('\n');
        
        if (_verbose)
        {
            Console.WriteLine($"  Performing basic dead code detection on {result.RelativePath}");
        }
        
        // Remove comments and strings to avoid false positives
        var cleanedCode = RemoveCommentsAndStrings(sourceCode);
        var cleanedLines = cleanedCode.Split('\n');
        
        // Find private methods that might be unused
        var privateMethodRegex = new Regex(@"private\s+(?:static\s+)?(?:\w+\s+)?(\w+)\s*\([^)]*\)", RegexOptions.Compiled);
        var internalMethodRegex = new Regex(@"internal\s+(?:static\s+)?(?:\w+\s+)?(\w+)\s*\([^)]*\)", RegexOptions.Compiled);
        var methodCalls = new HashSet<string>();
        
        // First pass: collect all method calls from cleaned code
        foreach (var line in cleanedLines)
        {
            // Skip method declarations themselves
            if (privateMethodRegex.IsMatch(line) || internalMethodRegex.IsMatch(line))
                continue;
                
            // Look for method calls (word followed by opening parenthesis)
            // But exclude method declarations
            var callMatches = Regex.Matches(line, @"(?<!(?:private|internal|public|protected|static)\s+(?:static\s+)?(?:\w+\s+)?)(\w+)\s*\(");
            foreach (Match match in callMatches)
            {
                var methodName = match.Groups[1].Value;
                // Skip common keywords and types
                if (!IsKeywordOrType(methodName))
                {
                    methodCalls.Add(methodName);
                }
            }
            
            // Also look for method references without parentheses (delegates, etc.)
            var refMatches = Regex.Matches(line, @"\.(\w+)\s*[;,\)\]\}]");
            foreach (Match match in refMatches)
            {
                var methodName = match.Groups[1].Value;
                if (!IsKeywordOrType(methodName))
                {
                    methodCalls.Add(methodName);
                }
            }
        }
        
        if (_verbose)
        {
            Console.WriteLine($"    Found {methodCalls.Count} method calls: {string.Join(", ", methodCalls.Take(10))}");
        }
        
        // Second pass: find private and internal methods that are not called
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            
            // Check private methods
            var privateMatch = privateMethodRegex.Match(line);
            if (privateMatch.Success)
            {
                var methodName = privateMatch.Groups[1].Value;
                
                if (_verbose)
                {
                    Console.WriteLine($"    Found private method: {methodName}");
                }
                
                // Skip special methods
                if (IsSpecialMethod(methodName))
                {
                    if (_verbose)
                    {
                        Console.WriteLine($"      Skipping special method: {methodName}");
                    }
                    continue;
                }
                
                if (!methodCalls.Contains(methodName))
                {
                    if (_verbose)
                    {
                        Console.WriteLine($"      DEAD CODE FOUND: {methodName} (not in call list)");
                    }
                    
                    var deadCodeItem = new DeadCodeItem
                    {
                        Name = methodName,
                        Type = "Method",
                        LineNumber = i + 1,
                        ColumnNumber = privateMatch.Index + 1,
                        ConfidencePercentage = 85, // Higher confidence for private methods
                        Reason = "Private method with no apparent references (basic analysis)"
                    };
                    
                    result.DeadMethods.Add(deadCodeItem);
                    result.PotentialDeadMethodCount++;
                }
                else if (_verbose)
                {
                    Console.WriteLine($"      Method {methodName} is used");
                }
            }
            
            // Check internal methods
            var internalMatch = internalMethodRegex.Match(line);
            if (internalMatch.Success)
            {
                var methodName = internalMatch.Groups[1].Value;
                
                if (_verbose)
                {
                    Console.WriteLine($"    Found internal method: {methodName}");
                }
                
                // Skip special methods
                if (IsSpecialMethod(methodName))
                {
                    if (_verbose)
                    {
                        Console.WriteLine($"      Skipping special method: {methodName}");
                    }
                    continue;
                }
                
                if (!methodCalls.Contains(methodName))
                {
                    if (_verbose)
                    {
                        Console.WriteLine($"      DEAD CODE FOUND: {methodName} (not in call list)");
                    }
                    
                    var deadCodeItem = new DeadCodeItem
                    {
                        Name = methodName,
                        Type = "Method",
                        LineNumber = i + 1,
                        ColumnNumber = internalMatch.Index + 1,
                        ConfidencePercentage = 70, // Medium confidence for internal methods
                        Reason = "Internal method with no apparent references (basic analysis)"
                    };
                    
                    result.DeadMethods.Add(deadCodeItem);
                    result.PotentialDeadMethodCount++;
                }
                else if (_verbose)
                {
                    Console.WriteLine($"      Method {methodName} is used");
                }
            }
        }
        
        // Find private classes that might be unused
        var privateClassRegex = new Regex(@"private\s+class\s+(\w+)", RegexOptions.Compiled);
        var internalClassRegex = new Regex(@"internal\s+class\s+(\w+)", RegexOptions.Compiled);
        var classUsages = new HashSet<string>();
        
        // Collect class usages from cleaned code
        foreach (var line in cleanedLines)
        {
            // Look for class instantiations, inheritance, type parameters, etc.
            var usageMatches = Regex.Matches(line, @"new\s+(\w+)\s*\(|:\s*(\w+)|<(\w+)>|(\w+)\s+\w+\s*[=;]");
            foreach (Match match in usageMatches)
            {
                for (int g = 1; g < match.Groups.Count; g++)
                {
                    if (match.Groups[g].Success && !string.IsNullOrWhiteSpace(match.Groups[g].Value))
                    {
                        classUsages.Add(match.Groups[g].Value);
                    }
                }
            }
        }
        
        if (_verbose)
        {
            Console.WriteLine($"    Found {classUsages.Count} class usages: {string.Join(", ", classUsages.Take(10))}");
        }
        
        // Find unused private and internal classes
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            
            // Check private classes
            var privateClassMatch = privateClassRegex.Match(line);
            if (privateClassMatch.Success)
            {
                var className = privateClassMatch.Groups[1].Value;
                
                if (_verbose)
                {
                    Console.WriteLine($"    Found private class: {className}");
                }
                
                if (!classUsages.Contains(className))
                {
                    if (_verbose)
                    {
                        Console.WriteLine($"      DEAD CODE FOUND: {className} (not in usage list)");
                    }
                    
                    var deadCodeItem = new DeadCodeItem
                    {
                        Name = className,
                        Type = "Class",
                        LineNumber = i + 1,
                        ColumnNumber = privateClassMatch.Index + 1,
                        ConfidencePercentage = 80, // High confidence for private classes
                        Reason = "Private class with no apparent references (basic analysis)"
                    };
                    
                    result.DeadClasses.Add(deadCodeItem);
                    result.PotentialDeadClassCount++;
                }
                else if (_verbose)
                {
                    Console.WriteLine($"      Class {className} is used");
                }
            }
            
            // Check internal classes (but only if they're not used externally)
            var internalClassMatch = internalClassRegex.Match(line);
            if (internalClassMatch.Success)
            {
                var className = internalClassMatch.Groups[1].Value;
                
                if (_verbose)
                {
                    Console.WriteLine($"    Found internal class: {className}");
                }
                
                if (!classUsages.Contains(className))
                {
                    if (_verbose)
                    {
                        Console.WriteLine($"      POTENTIALLY DEAD CODE: {className} (not in usage list)");
                    }
                    
                    var deadCodeItem = new DeadCodeItem
                    {
                        Name = className,
                        Type = "Class",
                        LineNumber = i + 1,
                        ColumnNumber = internalClassMatch.Index + 1,
                        ConfidencePercentage = 60, // Lower confidence for internal classes
                        Reason = "Internal class with no apparent references (basic analysis)"
                    };
                    
                    result.DeadClasses.Add(deadCodeItem);
                    result.PotentialDeadClassCount++;
                }
                else if (_verbose)
                {
                    Console.WriteLine($"      Class {className} is used");
                }
            }
        }
        
        if (_verbose)
        {
            Console.WriteLine($"    Dead code detection complete: {result.PotentialDeadMethodCount} dead methods, {result.PotentialDeadClassCount} dead classes");
        }
        
        await Task.CompletedTask;
    }
    
    private string RemoveCommentsAndStrings(string sourceCode)
    {
        // Remove single-line comments
        var withoutSingleLineComments = Regex.Replace(sourceCode, @"//.*$", "", RegexOptions.Multiline);
        
        // Remove multi-line comments
        var withoutMultiLineComments = Regex.Replace(withoutSingleLineComments, @"/\*.*?\*/", "", RegexOptions.Singleline);
        
        // Remove string literals (both single and double quotes)
        var withoutStrings = Regex.Replace(withoutMultiLineComments, @"""([^""\\]|\\.)*""|'([^'\\]|\\.)*'", "\"\"", RegexOptions.Singleline);
        
        return withoutStrings;
    }
    
    private bool IsSpecialMethod(string methodName)
    {
        return methodName == "Main" || 
               methodName.StartsWith("get_") || 
               methodName.StartsWith("set_") ||
               methodName.StartsWith("add_") ||
               methodName.StartsWith("remove_") ||
               methodName == "ToString" ||
               methodName == "GetHashCode" ||
               methodName == "Equals" ||
               methodName == "Dispose" ||
               methodName == "Finalize";
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
    
    private bool IsKeywordOrType(string methodName)
    {
        var keywords = new HashSet<string> 
        { 
            "void", "int", "string", "bool", "double", "float", "char", "object", 
            "var", "if", "else", "for", "foreach", "while", "do", "switch", "case",
            "try", "catch", "finally", "throw", "return", "break", "continue",
            "class", "interface", "struct", "enum", "namespace", "using",
            "public", "private", "protected", "internal", "static", "readonly",
            "const", "virtual", "override", "abstract", "sealed", "partial",
            "new", "this", "base", "null", "true", "false"
        };
        return keywords.Contains(methodName.ToLower());
    }
    
    private async Task PerformCrossFileDeadCodeAnalysis(
        Dictionary<string, string> allSourceCode, 
        List<FileAnalysisResult> allFileResults, 
        ProjectAnalysisResult projectResult)
    {
        if (_verbose)
        {
            Console.WriteLine($"Performing cross-file dead code analysis for project: {Path.GetFileName(projectResult.ProjectPath)}");
        }
        
        // Combine all source code for analysis
        var combinedCode = string.Join("\n", allSourceCode.Values);
        var cleanedCombinedCode = RemoveCommentsAndStrings(combinedCode);
        
        // Collect all declared classes and methods across all files
        var allDeclaredClasses = new Dictionary<string, (string filePath, int lineNumber)>();
        var allDeclaredMethods = new Dictionary<string, (string filePath, int lineNumber, string accessibility)>();
        var interfaceImplementations = new Dictionary<string, List<string>>(); // interface -> list of implementations
        
        // First pass: collect all declarations
        foreach (var kvp in allSourceCode)
        {
            var filePath = kvp.Key;
            var sourceCode = kvp.Value;
            var lines = sourceCode.Split('\n');
            
            // Find class declarations and their interfaces
            var classRegex = new Regex(@"(public|internal|private|protected)?\s+class\s+(\w+)(?:\s*:\s*([^{]+))?", RegexOptions.Compiled);
            for (int i = 0; i < lines.Length; i++)
            {
                var match = classRegex.Match(lines[i]);
                if (match.Success)
                {
                    var className = match.Groups[2].Value;
                    if (!allDeclaredClasses.ContainsKey(className))
                    {
                        allDeclaredClasses[className] = (filePath, i + 1);
                        
                        // Check if this class implements interfaces
                        if (match.Groups[3].Success)
                        {
                            var inheritanceList = match.Groups[3].Value;
                            var interfaces = inheritanceList.Split(',')
                                .Select(s => s.Trim())
                                .Where(s => s.StartsWith("I") && char.IsUpper(s[1])); // Interface naming convention
                            
                            foreach (var interfaceName in interfaces)
                            {
                                if (!interfaceImplementations.ContainsKey(interfaceName))
                                {
                                    interfaceImplementations[interfaceName] = new List<string>();
                                }
                                interfaceImplementations[interfaceName].Add(className);
                                
                                if (_verbose)
                                {
                                    Console.WriteLine($"    Found implementation: {className} implements {interfaceName}");
                                }
                            }
                        }
                    }
                }
            }
            
            // Find method declarations
            var methodRegex = new Regex(@"(public|internal|private|protected)\s+(?:static\s+)?(?:async\s+)?(?:\w+(?:<[^>]+>)?\s+)?(\w+)(?:<[^>]+>)?\s*\([^)]*\)", RegexOptions.Compiled);
            for (int i = 0; i < lines.Length; i++)
            {
                var match = methodRegex.Match(lines[i]);
                if (match.Success)
                {
                    var accessibility = match.Groups[1].Value;
                    var methodName = match.Groups[2].Value;
                    
                    // Check if this is an extension method
                    var isExtensionMethod = lines[i].Contains("this ") && lines[i].Contains("static");
                    
                    if (_verbose)
                    {
                        var methodType = isExtensionMethod ? "extension method" : "method";
                        Console.WriteLine($"    Found {methodType}: {accessibility} {methodName} in {Path.GetFileName(filePath)}:{i + 1}");
                    }
                    
                    // Skip special methods
                    if (!IsSpecialMethod(methodName) && !IsKeywordOrType(methodName))
                    {
                        var key = $"{methodName}_{accessibility}";
                        if (!allDeclaredMethods.ContainsKey(key))
                        {
                            allDeclaredMethods[key] = (filePath, i + 1, accessibility);
                            
                            if (_verbose)
                            {
                                Console.WriteLine($"      Added to analysis: {key}");
                            }
                        }
                    }
                    else if (_verbose)
                    {
                        Console.WriteLine($"      Skipped special/keyword method: {methodName}");
                    }
                }
            }
        }
        
        if (_verbose)
        {
            Console.WriteLine($"Found {allDeclaredClasses.Count} classes and {allDeclaredMethods.Count} methods to analyze");
        }
        
        // Second pass: check usage across all files
        var usedClasses = new HashSet<string>();
        var usedMethods = new HashSet<string>();
        
        // Mark framework-related classes as used (they're typically instantiated by frameworks)
        foreach (var className in allDeclaredClasses.Keys)
        {
            if (IsFrameworkClass(className))
            {
                usedClasses.Add(className);
                if (_verbose)
                {
                    Console.WriteLine($"    Framework class {className} marked as USED (framework pattern)");
                }
            }
        }
        
        // Create a version of the code without method declarations to avoid false positives
        var codeWithoutDeclarations = cleanedCombinedCode;
        
        // Remove method declarations from the analysis
        var methodDeclarationRegex = new Regex(@"(public|internal|private|protected)\s+(?:static\s+)?(?:async\s+)?(?:\w+(?:<[^>]+>)?\s+)?(\w+)(?:<[^>]+>)?\s*\([^)]*\)\s*\{?", RegexOptions.Compiled);
        codeWithoutDeclarations = methodDeclarationRegex.Replace(codeWithoutDeclarations, "");
        
        // Remove class declarations too
        var classDeclarationRegex = new Regex(@"(public|internal|private|protected)?\s+class\s+(\w+)", RegexOptions.Compiled);
        codeWithoutDeclarations = classDeclarationRegex.Replace(codeWithoutDeclarations, "");
        
        if (_verbose)
        {
            Console.WriteLine($"Analyzing usage in cleaned code (length: {codeWithoutDeclarations.Length} chars)");
        }
        
        // Analyze usage patterns in cleaned combined code
        foreach (var className in allDeclaredClasses.Keys)
        {
            // Look for class usage patterns
            var classUsagePatterns = new[]
            {
                $@"\bnew\s+{Regex.Escape(className)}\s*\(",  // new ClassName()
                $@"\b{Regex.Escape(className)}\s+\w+",      // ClassName variable
                $@":\s*{Regex.Escape(className)}\b",        // inheritance
                $@"<{Regex.Escape(className)}>",            // generic parameter
                $@"\({Regex.Escape(className)}\s+",         // parameter type
                $@"\b{Regex.Escape(className)}\.",          // static access
                $@"I{Regex.Escape(className)}\b",           // Interface reference (IClassName)
                $@"{Regex.Escape(className)}\s*>",          // Generic constraint
                // Enhanced Dependency Injection patterns
                $@"Add\w*<[^,>]*,\s*{Regex.Escape(className)}\s*>",    // services.AddScoped<IService, Service>()
                $@"Add\w*<{Regex.Escape(className)}\s*>",              // services.AddScoped<Service>()
                $@"Add\w*\(\s*typeof\s*\(\s*{Regex.Escape(className)}\s*\)",  // services.AddScoped(typeof(Service))
                $@"Register\w*<[^,>]*,\s*{Regex.Escape(className)}\s*>",       // container.Register<IService, Service>()
                $@"Register\w*<{Regex.Escape(className)}\s*>",                 // container.Register<Service>()
                $@"Register\w*\(\s*typeof\s*\(\s*{Regex.Escape(className)}\s*\)",  // container.Register(typeof(Service))
                $@"Bind\w*<[^,>]*>\s*\.To\w*<{Regex.Escape(className)}\s*>",   // kernel.Bind<IService>().To<Service>()
                $@"Bind\w*<{Regex.Escape(className)}\s*>",                     // kernel.Bind<Service>()
                $@"For\w*<[^,>]*>\s*\.Use\w*<{Regex.Escape(className)}\s*>",   // StructureMap: For<IService>().Use<Service>()
                $@"Configure\w*<{Regex.Escape(className)}\s*>",                // services.Configure<Options>()
                $@"TryAdd\w*<[^,>]*,\s*{Regex.Escape(className)}\s*>",        // services.TryAddScoped<IService, Service>()
                $@"TryAdd\w*<{Regex.Escape(className)}\s*>",                   // services.TryAddScoped<Service>()
                // Generic type constraints and parameters
                $@"where\s+\w+\s*:\s*[^,]*{Regex.Escape(className)}",          // where T : IService
                $@"typeof\s*\(\s*{Regex.Escape(className)}\s*\)",              // typeof(Service)
                // Constructor injection patterns
                $@"\(\s*{Regex.Escape(className)}\s+\w+",                      // constructor parameter
                // Method injection patterns
                $@"GetService<{Regex.Escape(className)}\s*>",                  // serviceProvider.GetService<Service>()
                $@"GetRequiredService<{Regex.Escape(className)}\s*>",          // serviceProvider.GetRequiredService<Service>()
                $@"GetServices<{Regex.Escape(className)}\s*>",                 // serviceProvider.GetServices<Service>()
                $@"Resolve<{Regex.Escape(className)}\s*>",                     // container.Resolve<Service>()
                $@"ResolveAll<{Regex.Escape(className)}\s*>",                  // container.ResolveAll<Service>()
                // Attribute-based injection
                $@"\[Inject\].*{Regex.Escape(className)}\s+\w+",               // [Inject] Service service
                $@"\[Dependency\].*{Regex.Escape(className)}\s+\w+",           // [Dependency] Service service
            };
            
            foreach (var pattern in classUsagePatterns)
            {
                if (Regex.IsMatch(codeWithoutDeclarations, pattern, RegexOptions.IgnoreCase))
                {
                    usedClasses.Add(className);
                    
                    // If this is an interface being used, mark its implementations as used too
                    if (interfaceImplementations.ContainsKey(className))
                    {
                        foreach (var implementation in interfaceImplementations[className])
                        {
                            usedClasses.Add(implementation);
                            if (_verbose)
                            {
                                Console.WriteLine($"    Interface {className} is used, marking implementation {implementation} as USED");
                            }
                        }
                    }
                    
                    if (_verbose)
                    {
                        Console.WriteLine($"    Class {className} marked as USED (pattern matched: {pattern})");
                    }
                    break;
                }
            }
        }
        
        foreach (var methodKey in allDeclaredMethods.Keys)
        {
            var methodName = methodKey.Split('_')[0];
            
            // Look for method call patterns
            var methodUsagePatterns = new[]
            {
                $@"\b{Regex.Escape(methodName)}\s*\(",      // methodName()
                $@"\.{Regex.Escape(methodName)}\s*\(",     // .methodName() - extension method calls
                $@"\b{Regex.Escape(methodName)}\s*;",      // delegate reference
                $@"=>\s*{Regex.Escape(methodName)}\s*\(",  // lambda expression
                $@"\w+\.{Regex.Escape(methodName)}\s*\(",  // object.ExtensionMethod() - specific for extension methods
                $@"\.{Regex.Escape(methodName)}\(",        // .ExtensionMethod( - without space
            };
            
            foreach (var pattern in methodUsagePatterns)
            {
                if (Regex.IsMatch(codeWithoutDeclarations, pattern, RegexOptions.IgnoreCase))
                {
                    usedMethods.Add(methodKey);
                    
                    if (_verbose)
                    {
                        Console.WriteLine($"    Method {methodName} marked as USED (pattern: {pattern})");
                    }
                    break;
                }
            }
            
            if (_verbose && !usedMethods.Contains(methodKey))
            {
                Console.WriteLine($"    Method {methodName} marked as UNUSED");
            }
        }
        
        if (_verbose)
        {
            Console.WriteLine($"Found {usedClasses.Count} used classes and {usedMethods.Count} used methods");
        }
        
        // Third pass: identify dead code and update file results
        foreach (var fileResult in allFileResults)
        {
            // Clear previous dead code results
            fileResult.DeadClasses.Clear();
            fileResult.DeadMethods.Clear();
            fileResult.PotentialDeadClassCount = 0;
            fileResult.PotentialDeadMethodCount = 0;
            
            // Check for dead classes in this file
            foreach (var kvp in allDeclaredClasses)
            {
                var className = kvp.Key;
                var (filePath, lineNumber) = kvp.Value;
                
                if (filePath == fileResult.FilePath && !usedClasses.Contains(className))
                {
                    var deadCodeItem = new DeadCodeItem
                    {
                        Name = className,
                        Type = "Class",
                        LineNumber = lineNumber,
                        ColumnNumber = 1,
                        ConfidencePercentage = 85,
                        Reason = "Class with no apparent references across the project"
                    };
                    
                    fileResult.DeadClasses.Add(deadCodeItem);
                    fileResult.PotentialDeadClassCount++;
                    
                    if (_verbose)
                    {
                        Console.WriteLine($"DEAD CLASS: {className} in {Path.GetFileName(filePath)}:{lineNumber}");
                    }
                }
            }
            
            // Check for dead methods in this file
            foreach (var kvp in allDeclaredMethods)
            {
                var methodKey = kvp.Key;
                var (filePath, lineNumber, accessibility) = kvp.Value;
                var methodName = methodKey.Split('_')[0];
                
                if (filePath == fileResult.FilePath && !usedMethods.Contains(methodKey))
                {
                    // Flag private, internal, and public methods as potential dead code
                    if (accessibility == "private" || accessibility == "internal" || accessibility == "public")
                    {
                        var confidence = accessibility switch
                        {
                            "private" => 90,
                            "internal" => 75,
                            "public" => 60, // Lower confidence for public methods as they might be part of API
                            _ => 50
                        };
                        
                        var deadCodeItem = new DeadCodeItem
                        {
                            Name = methodName,
                            Type = "Method",
                            LineNumber = lineNumber,
                            ColumnNumber = 1,
                            ConfidencePercentage = confidence,
                            Reason = $"{accessibility.Substring(0, 1).ToUpper()}{accessibility.Substring(1)} method with no apparent references across the project"
                        };
                        
                        fileResult.DeadMethods.Add(deadCodeItem);
                        fileResult.PotentialDeadMethodCount++;
                        
                        if (_verbose)
                        {
                            Console.WriteLine($"DEAD METHOD: {accessibility} {methodName} in {Path.GetFileName(filePath)}:{lineNumber}");
                        }
                    }
                }
            }
            
            // Update project totals
            projectResult.PotentialDeadMethods += fileResult.PotentialDeadMethodCount;
            projectResult.PotentialDeadClasses += fileResult.PotentialDeadClassCount;
        }
        
        await Task.CompletedTask;
    }
    
    private async Task PerformCrossProjectAnalysis(List<string> projectPaths, AnalysisResult result)
    {
        if (_verbose)
        {
            Console.WriteLine($"Performing cross-project analysis for {projectPaths.Count} projects");
        }
        
        // Filter out test projects if requested
        var filteredProjectPaths = projectPaths;
        if (_ignoreTests)
        {
            filteredProjectPaths = projectPaths.Where(p => !IsTestProject(p)).ToList();
            
            if (_verbose)
            {
                var ignoredCount = projectPaths.Count - filteredProjectPaths.Count;
                if (ignoredCount > 0)
                {
                    Console.WriteLine($"Ignoring {ignoredCount} test project(s)");
                }
            }
        }
        
        if (filteredProjectPaths.Count == 0)
        {
            if (_verbose)
            {
                Console.WriteLine("No projects to analyze after filtering");
            }
            return;
        }
        
        // Collect all source code from all projects
        var allSourceCode = new Dictionary<string, string>();
        var allProjectResults = new List<ProjectAnalysisResult>();
        
        // First pass: collect all source files from all projects
        foreach (var projectPath in filteredProjectPaths)
        {
            var projectDir = Path.GetDirectoryName(projectPath);
            var sourceFiles = Directory.GetFiles(projectDir!, "*.cs", SearchOption.AllDirectories);
            
            var projectResult = new ProjectAnalysisResult
            {
                ProjectPath = projectPath,
                SourceFileCount = sourceFiles.Length
            };
            
            if (_verbose)
            {
                Console.WriteLine($"  Collecting files from {Path.GetFileName(projectPath)}: {sourceFiles.Length} files");
            }
            
            foreach (var sourceFile in sourceFiles)
            {
                // Skip generated files
                if (IsGeneratedFile(sourceFile))
                {
                    continue;
                }
                
                // Skip migration files if requested
                if (_ignoreMigrations && IsMigrationFile(sourceFile))
                {
                    if (_verbose)
                    {
                        Console.WriteLine($"    Skipping migration file: {sourceFile}");
                    }
                    continue;
                }
                
                // Skip Azure Function files if requested
                if (_ignoreAzureFunctions && IsAzureFunctionFile(sourceFile))
                {
                    if (_verbose)
                    {
                        Console.WriteLine($"    Skipping Azure Function file: {sourceFile}");
                    }
                    continue;
                }
                
                // Skip Controller files if requested
                if (_ignoreControllers && IsControllerFile(sourceFile))
                {
                    if (_verbose)
                    {
                        Console.WriteLine($"    Skipping Controller file: {sourceFile}");
                    }
                    continue;
                }
                
                try
                {
                    var sourceCode = await File.ReadAllTextAsync(sourceFile);
                    allSourceCode[sourceFile] = sourceCode;
                    
                    var fileResult = await AnalyzeSourceFileAsync(sourceFile);
                    projectResult.FileResults.Add(fileResult);
                    
                    // Aggregate statistics
                    projectResult.TotalMethods += fileResult.MethodCount;
                    projectResult.TotalClasses += fileResult.ClassCount;
                }
                catch (Exception ex)
                {
                    if (_verbose)
                    {
                        Console.WriteLine($"    Error reading file {sourceFile}: {ex.Message}");
                    }
                }
            }
            
            allProjectResults.Add(projectResult);
        }
        
        if (_verbose)
        {
            Console.WriteLine($"Collected {allSourceCode.Count} source files from {projectPaths.Count} projects");
        }
        
        // Second pass: perform cross-project dead code analysis
        await PerformCrossProjectDeadCodeAnalysis(allSourceCode, allProjectResults);
        
        // Add all project results to the main result
        result.ProjectResults.AddRange(allProjectResults);
    }
    
    private async Task PerformCrossProjectDeadCodeAnalysis(
        Dictionary<string, string> allSourceCode, 
        List<ProjectAnalysisResult> allProjectResults)
    {
        if (_verbose)
        {
            Console.WriteLine($"Performing cross-project dead code analysis across {allSourceCode.Count} files");
        }
        
        // Combine all source code for analysis
        var combinedCode = string.Join("\n", allSourceCode.Values);
        var cleanedCombinedCode = RemoveCommentsAndStrings(combinedCode);
        
        // Collect all declared classes and methods across ALL projects
        var allDeclaredClasses = new Dictionary<string, (string filePath, int lineNumber, string projectPath)>();
        var allDeclaredMethods = new Dictionary<string, (string filePath, int lineNumber, string accessibility, string projectPath)>();
        var interfaceImplementations = new Dictionary<string, List<string>>(); // interface -> list of implementations
        
        // First pass: collect all declarations from all projects
        foreach (var kvp in allSourceCode)
        {
            var filePath = kvp.Key;
            var sourceCode = kvp.Value;
            var lines = sourceCode.Split('\n');
            
            // Find which project this file belongs to
            var projectPath = allProjectResults.FirstOrDefault(p => filePath.StartsWith(Path.GetDirectoryName(p.ProjectPath)!))?.ProjectPath ?? "Unknown";
            
            // Find class declarations and their interfaces
            var classRegex = new Regex(@"(public|internal|private|protected)?\s+class\s+(\w+)(?:\s*:\s*([^{]+))?", RegexOptions.Compiled);
            for (int i = 0; i < lines.Length; i++)
            {
                var match = classRegex.Match(lines[i]);
                if (match.Success)
                {
                    var className = match.Groups[2].Value;
                    var key = $"{className}_{Path.GetFileName(filePath)}"; // Make unique per file
                    if (!allDeclaredClasses.ContainsKey(key))
                    {
                        allDeclaredClasses[key] = (filePath, i + 1, projectPath);
                        
                        // Check if this class implements interfaces
                        if (match.Groups[3].Success)
                        {
                            var inheritanceList = match.Groups[3].Value;
                            var interfaces = inheritanceList.Split(',')
                                .Select(s => s.Trim())
                                .Where(s => s.StartsWith("I") && char.IsUpper(s[1])); // Interface naming convention
                            
                            foreach (var interfaceName in interfaces)
                            {
                                if (!interfaceImplementations.ContainsKey(interfaceName))
                                {
                                    interfaceImplementations[interfaceName] = new List<string>();
                                }
                                interfaceImplementations[interfaceName].Add(className);
                                
                                if (_verbose)
                                {
                                    Console.WriteLine($"    Found implementation: {className} implements {interfaceName}");
                                }
                            }
                        }
                    }
                }
            }
            
            // Find method declarations
            var methodRegex = new Regex(@"(public|internal|private|protected)\s+(?:static\s+)?(?:async\s+)?(?:\w+(?:<[^>]+>)?\s+)?(\w+)(?:<[^>]+>)?\s*\([^)]*\)", RegexOptions.Compiled);
            for (int i = 0; i < lines.Length; i++)
            {
                var match = methodRegex.Match(lines[i]);
                if (match.Success)
                {
                    var accessibility = match.Groups[1].Value;
                    var methodName = match.Groups[2].Value;
                    
                    // Check if this is an extension method
                    var isExtensionMethod = lines[i].Contains("this ") && lines[i].Contains("static");
                    
                    if (_verbose)
                    {
                        var methodType = isExtensionMethod ? "extension method" : "method";
                        Console.WriteLine($"    Found {methodType}: {accessibility} {methodName} in {Path.GetFileName(filePath)}:{i + 1}");
                    }
                    
                    // Skip special methods
                    if (!IsSpecialMethod(methodName) && !IsKeywordOrType(methodName))
                    {
                        var key = $"{methodName}_{accessibility}";
                        if (!allDeclaredMethods.ContainsKey(key))
                        {
                            allDeclaredMethods[key] = (filePath, i + 1, accessibility, projectPath);
                        }
                    }
                }
            }
        }
        
        if (_verbose)
        {
            Console.WriteLine($"Found {allDeclaredClasses.Count} classes and {allDeclaredMethods.Count} methods across all projects");
        }
        
        // Create a version of the code without method/class declarations to avoid false positives
        var codeWithoutDeclarations = cleanedCombinedCode;
        
        // Remove method declarations from the analysis
        var methodDeclarationRegex = new Regex(@"(public|internal|private|protected)\s+(?:static\s+)?(?:async\s+)?(?:\w+(?:<[^>]+>)?\s+)?(\w+)(?:<[^>]+>)?\s*\([^)]*\)\s*\{?", RegexOptions.Compiled);
        codeWithoutDeclarations = methodDeclarationRegex.Replace(codeWithoutDeclarations, "");
        
        // Remove class declarations too
        var classDeclarationRegex = new Regex(@"(public|internal|private|protected)?\s+class\s+(\w+)", RegexOptions.Compiled);
        codeWithoutDeclarations = classDeclarationRegex.Replace(codeWithoutDeclarations, "");
        
        // Second pass: check usage across ALL projects
        var usedClasses = new HashSet<string>();
        var usedMethods = new HashSet<string>();
        
        // Mark framework-related classes as used (they're typically instantiated by frameworks)
        foreach (var classKey in allDeclaredClasses.Keys)
        {
            var className = classKey.Split('_')[0];
            if (IsFrameworkClass(className))
            {
                usedClasses.Add(classKey);
                if (_verbose)
                {
                    Console.WriteLine($"    Framework class {className} marked as USED (framework pattern)");
                }
            }
        }
        
        // Analyze class usage patterns
        foreach (var classKey in allDeclaredClasses.Keys)
        {
            var className = classKey.Split('_')[0];
            
            // Look for class usage patterns
            var classUsagePatterns = new[]
            {
                $@"\bnew\s+{Regex.Escape(className)}\s*\(",  // new ClassName()
                $@"\b{Regex.Escape(className)}\s+\w+",      // ClassName variable
                $@":\s*{Regex.Escape(className)}\b",        // inheritance
                $@"<{Regex.Escape(className)}>",            // generic parameter
                $@"\({Regex.Escape(className)}\s+",         // parameter type
                $@"\b{Regex.Escape(className)}\.",          // static access
                $@"I{Regex.Escape(className)}\b",           // Interface reference (IClassName)
                $@"{Regex.Escape(className)}\s*>",          // Generic constraint
                // Enhanced Dependency Injection patterns
                $@"Add\w*<[^,>]*,\s*{Regex.Escape(className)}\s*>",    // services.AddScoped<IService, Service>()
                $@"Add\w*<{Regex.Escape(className)}\s*>",              // services.AddScoped<Service>()
                $@"Add\w*\(\s*typeof\s*\(\s*{Regex.Escape(className)}\s*\)",  // services.AddScoped(typeof(Service))
                $@"Register\w*<[^,>]*,\s*{Regex.Escape(className)}\s*>",       // container.Register<IService, Service>()
                $@"Register\w*<{Regex.Escape(className)}\s*>",                 // container.Register<Service>()
                $@"Register\w*\(\s*typeof\s*\(\s*{Regex.Escape(className)}\s*\)",  // container.Register(typeof(Service))
                $@"Bind\w*<[^,>]*>\s*\.To\w*<{Regex.Escape(className)}\s*>",   // kernel.Bind<IService>().To<Service>()
                $@"Bind\w*<{Regex.Escape(className)}\s*>",                     // kernel.Bind<Service>()
                $@"For\w*<[^,>]*>\s*\.Use\w*<{Regex.Escape(className)}\s*>",   // StructureMap: For<IService>().Use<Service>()
                $@"Configure\w*<{Regex.Escape(className)}\s*>",                // services.Configure<Options>()
                $@"TryAdd\w*<[^,>]*,\s*{Regex.Escape(className)}\s*>",        // services.TryAddScoped<IService, Service>()
                $@"TryAdd\w*<{Regex.Escape(className)}\s*>",                   // services.TryAddScoped<Service>()
                // Generic type constraints and parameters
                $@"where\s+\w+\s*:\s*[^,]*{Regex.Escape(className)}",          // where T : IService
                $@"typeof\s*\(\s*{Regex.Escape(className)}\s*\)",              // typeof(Service)
                // Constructor injection patterns
                $@"\(\s*{Regex.Escape(className)}\s+\w+",                      // constructor parameter
                // Method injection patterns
                $@"GetService<{Regex.Escape(className)}\s*>",                  // serviceProvider.GetService<Service>()
                $@"GetRequiredService<{Regex.Escape(className)}\s*>",          // serviceProvider.GetRequiredService<Service>()
                $@"GetServices<{Regex.Escape(className)}\s*>",                 // serviceProvider.GetServices<Service>()
                $@"Resolve<{Regex.Escape(className)}\s*>",                     // container.Resolve<Service>()
                $@"ResolveAll<{Regex.Escape(className)}\s*>",                  // container.ResolveAll<Service>()
                // Attribute-based injection
                $@"\[Inject\].*{Regex.Escape(className)}\s+\w+",               // [Inject] Service service
                $@"\[Dependency\].*{Regex.Escape(className)}\s+\w+",           // [Dependency] Service service
            };
            
            foreach (var pattern in classUsagePatterns)
            {
                if (Regex.IsMatch(codeWithoutDeclarations, pattern, RegexOptions.IgnoreCase))
                {
                    usedClasses.Add(classKey);
                    
                    // If this is an interface being used, mark its implementations as used too
                    if (interfaceImplementations.ContainsKey(className))
                    {
                        foreach (var implementation in interfaceImplementations[className])
                        {
                            var implKey = $"{implementation}_{Path.GetFileName(allDeclaredClasses.FirstOrDefault(kvp => kvp.Key.StartsWith(implementation)).Value.filePath ?? "")}";
                            usedClasses.Add(implKey);
                            if (_verbose)
                            {
                                Console.WriteLine($"    Interface {className} is used, marking implementation {implementation} as USED");
                            }
                        }
                    }
                    
                    if (_verbose)
                    {
                        Console.WriteLine($"    Class {className} marked as USED (pattern matched: {pattern})");
                    }
                    break;
                }
            }
        }
        
        // Analyze method usage patterns
        foreach (var methodKey in allDeclaredMethods.Keys)
        {
            var methodName = methodKey.Split('_')[0];
            
            // Look for method call patterns
            var methodUsagePatterns = new[]
            {
                $@"\b{Regex.Escape(methodName)}\s*\(",      // methodName()
                $@"\.{Regex.Escape(methodName)}\s*\(",     // .methodName() - extension method calls
                $@"\b{Regex.Escape(methodName)}\s*;",      // delegate reference
                $@"=>\s*{Regex.Escape(methodName)}\s*\(",  // lambda expression
                $@"\w+\.{Regex.Escape(methodName)}\s*\(",  // object.ExtensionMethod() - specific for extension methods
                $@"\.{Regex.Escape(methodName)}\(",        // .ExtensionMethod( - without space
            };
            
            foreach (var pattern in methodUsagePatterns)
            {
                if (Regex.IsMatch(codeWithoutDeclarations, pattern, RegexOptions.IgnoreCase))
                {
                    usedMethods.Add(methodKey);
                    if (_verbose)
                    {
                        Console.WriteLine($"    Method {methodName} marked as USED (cross-project)");
                    }
                    break;
                }
            }
        }
        
        if (_verbose)
        {
            Console.WriteLine($"Cross-project analysis: {usedClasses.Count} used classes, {usedMethods.Count} used methods");
        }
        
        // Third pass: identify dead code and update project results
        foreach (var projectResult in allProjectResults)
        {
            foreach (var fileResult in projectResult.FileResults)
            {
                // Clear previous dead code results
                fileResult.DeadClasses.Clear();
                fileResult.DeadMethods.Clear();
                fileResult.PotentialDeadClassCount = 0;
                fileResult.PotentialDeadMethodCount = 0;
                
                // Check for dead classes in this file
                foreach (var kvp in allDeclaredClasses)
                {
                    var classKey = kvp.Key;
                    var (filePath, lineNumber, projectPath) = kvp.Value;
                    var className = classKey.Split('_')[0];
                    
                    if (filePath == fileResult.FilePath && !usedClasses.Contains(classKey))
                    {
                        var deadCodeItem = new DeadCodeItem
                        {
                            Name = className,
                            Type = "Class",
                            LineNumber = lineNumber,
                            ColumnNumber = 1,
                            ConfidencePercentage = 85,
                            Reason = "Class with no apparent references across all projects in the solution"
                        };
                        
                        fileResult.DeadClasses.Add(deadCodeItem);
                        fileResult.PotentialDeadClassCount++;
                        
                        if (_verbose)
                        {
                            Console.WriteLine($"DEAD CLASS (cross-project): {className} in {Path.GetFileName(filePath)}:{lineNumber}");
                        }
                    }
                }
                
                // Check for dead methods in this file
                foreach (var kvp in allDeclaredMethods)
                {
                    var methodKey = kvp.Key;
                    var (filePath, lineNumber, accessibility, projectPath) = kvp.Value;
                    var methodName = methodKey.Split('_')[0];
                    
                    if (filePath == fileResult.FilePath && !usedMethods.Contains(methodKey))
                    {
                        // Flag private, internal, and public methods as potential dead code
                        if (accessibility == "private" || accessibility == "internal" || accessibility == "public")
                        {
                            var confidence = accessibility switch
                            {
                                "private" => 90,
                                "internal" => 75,
                                "public" => 60, // Lower confidence for public methods as they might be part of API
                                _ => 50
                            };
                            
                            var deadCodeItem = new DeadCodeItem
                            {
                                Name = methodName,
                                Type = "Method",
                                LineNumber = lineNumber,
                                ColumnNumber = 1,
                                ConfidencePercentage = confidence,
                                Reason = $"{accessibility.Substring(0, 1).ToUpper()}{accessibility.Substring(1)} method with no apparent references across all projects in the solution"
                            };
                            
                            fileResult.DeadMethods.Add(deadCodeItem);
                            fileResult.PotentialDeadMethodCount++;
                            
                            if (_verbose)
                            {
                                Console.WriteLine($"DEAD METHOD (cross-project): {accessibility} {methodName} in {Path.GetFileName(filePath)}:{lineNumber}");
                            }
                        }
                    }
                }
                
                // Update project totals
                projectResult.PotentialDeadMethods += fileResult.PotentialDeadMethodCount;
                projectResult.PotentialDeadClasses += fileResult.PotentialDeadClassCount;
            }
        }
        
        await Task.CompletedTask;
    }
    
    /// <summary>
    /// Determines if a project is a test project based on naming conventions and package references
    /// </summary>
    /// <param name="projectPath">Path to the project file</param>
    /// <returns>True if the project appears to be a test project</returns>
    private bool IsTestProject(string projectPath)
    {
        try
        {
            var projectName = Path.GetFileNameWithoutExtension(projectPath);
            
            // Check common test project naming patterns
            var testPatterns = new[]
            {
                "test", "tests", "unittest", "unittests", "integrationtest", "integrationtests",
                "spec", "specs", "fixture", "fixtures"
            };
            
            foreach (var pattern in testPatterns)
            {
                if (projectName.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    if (_verbose)
                    {
                        Console.WriteLine($"  Detected test project by name: {projectName}");
                    }
                    return true;
                }
            }
            
            // Check project file content for test-related package references
            var projectContent = File.ReadAllText(projectPath);
            var testPackages = new[]
            {
                "Microsoft.NET.Test.Sdk",
                "xunit", "NUnit", "MSTest",
                "FluentAssertions", "Moq", "NSubstitute",
                "Shouldly", "AutoFixture"
            };
            
            foreach (var package in testPackages)
            {
                if (projectContent.Contains(package, StringComparison.OrdinalIgnoreCase))
                {
                    if (_verbose)
                    {
                        Console.WriteLine($"  Detected test project by package reference: {projectName} (contains {package})");
                    }
                    return true;
                }
            }
            
            return false;
        }
        catch (Exception ex)
        {
            if (_verbose)
            {
                Console.WriteLine($"  Error checking if project is test project {projectPath}: {ex.Message}");
            }
            return false;
        }
    }
    
    /// <summary>
    /// Determines if a class follows framework patterns and should be considered as used
    /// </summary>
    /// <param name="className">Name of the class to check</param>
    /// <returns>True if the class follows framework patterns</returns>
    private bool IsFrameworkClass(string className)
    {
        // Common framework class patterns
        var frameworkPatterns = new[]
        {
            "Controller",     // MVC/API Controllers
            "Service",        // Services
            "Repository",     // Repository pattern
            "Handler",        // Command/Query handlers
            "Middleware",     // ASP.NET middleware
            "Filter",         // Action filters
            "Attribute",      // Custom attributes
            "Module",         // Modules
            "Startup",        // Application startup
            "Program",        // Program entry point
            "Configuration",  // Configuration classes
            "Options",        // Options pattern
            "Settings",       // Settings classes
            "Provider",       // Provider pattern
            "Factory",        // Factory pattern
            "Builder",        // Builder pattern
            "Validator",      // Validation classes
            "Mapper",         // Mapping classes
            "Profile",        // AutoMapper profiles
            "Hub",            // SignalR hubs
            "Worker",         // Background workers
            "Job",            // Background jobs
            "Task",           // Task classes
            "Command",        // Command pattern
            "Query",          // Query pattern
            "Event",          // Event classes
            "Dto",            // Data transfer objects
            "ViewModel",      // View models
            "Model",          // Models
            "Entity",         // Entity classes
            "DbContext",      // Entity Framework contexts
            "Migration",      // Database migrations
        };
        
        return frameworkPatterns.Any(pattern => 
            className.EndsWith(pattern, StringComparison.OrdinalIgnoreCase) ||
            className.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }
    
    /// <summary>
    /// Determines if a file is a database migration file
    /// </summary>
    /// <param name="filePath">Path to the file to check</param>
    /// <returns>True if the file appears to be a migration file</returns>
    private bool IsMigrationFile(string filePath)
    {
        try
        {
            var fileName = Path.GetFileName(filePath);
            var directoryName = Path.GetDirectoryName(filePath);
            
            // Check for common migration file patterns
            var migrationPatterns = new[]
            {
                "Migration", "migration", "Migrations", "migrations",
                "_CreateTable", "_AddColumn", "_DropTable", "_AlterTable",
                "_Initial", "_InitialCreate"
            };
            
            // Check filename patterns
            foreach (var pattern in migrationPatterns)
            {
                if (fileName.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            
            // Check directory patterns
            if (directoryName != null)
            {
                var dirPatterns = new[] { "Migrations", "migrations", "Migration", "migration" };
                foreach (var pattern in dirPatterns)
                {
                    if (directoryName.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            
            // Check file content for migration-specific patterns
            if (File.Exists(filePath))
            {
                var content = File.ReadAllText(filePath);
                var contentPatterns = new[]
                {
                    "DbMigration", "Migration", "migrationBuilder", "MigrationBuilder",
                    "CreateTable", "DropTable", "AddColumn", "DropColumn",
                    "FluentMigrator", "EntityFramework"
                };
                
                foreach (var pattern in contentPatterns)
                {
                    if (content.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            
            return false;
        }
        catch (Exception ex)
        {
            if (_verbose)
            {
                Console.WriteLine($"Error checking if file is migration: {filePath}: {ex.Message}");
            }
            return false;
        }
    }
    
    /// <summary>
    /// Determines if a file is an Azure Function file
    /// </summary>
    /// <param name="filePath">Path to the file to check</param>
    /// <returns>True if the file appears to be an Azure Function file</returns>
    private bool IsAzureFunctionFile(string filePath)
    {
        try
        {
            var fileName = Path.GetFileName(filePath);
            
            // Check for common Azure Function file patterns
            var functionPatterns = new[]
            {
                "Function", "function", "Functions", "functions",
                "AzureFunction", "azurefunction", "AzureFunctions", "azurefunctions"
            };
            
            // Check filename patterns
            foreach (var pattern in functionPatterns)
            {
                if (fileName.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            
            // Check file content for Azure Function-specific patterns
            if (File.Exists(filePath))
            {
                var content = File.ReadAllText(filePath);
                var contentPatterns = new[]
                {
                    "[FunctionName", "FunctionName", "Microsoft.Azure.WebJobs",
                    "Microsoft.Azure.Functions", "HttpTrigger", "TimerTrigger",
                    "BlobTrigger", "QueueTrigger", "ServiceBusTrigger",
                    "CosmosDBTrigger", "EventHubTrigger", "EventGridTrigger"
                };
                
                foreach (var pattern in contentPatterns)
                {
                    if (content.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            
            return false;
        }
        catch (Exception ex)
        {
            if (_verbose)
            {
                Console.WriteLine($"Error checking if file is Azure Function: {filePath}: {ex.Message}");
            }
            return false;
        }
    }
    
    /// <summary>
    /// Determines if a file is a Controller file
    /// </summary>
    /// <param name="filePath">Path to the file to check</param>
    /// <returns>True if the file appears to be a Controller file</returns>
    private bool IsControllerFile(string filePath)
    {
        try
        {
            var fileName = Path.GetFileName(filePath);
            
            // Check for common Controller file patterns
            if (fileName.EndsWith("Controller.cs", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            
            // Check file content for Controller-specific patterns
            if (File.Exists(filePath))
            {
                var content = File.ReadAllText(filePath);
                var contentPatterns = new[]
                {
                    ": Controller", ": ControllerBase", ": ApiController",
                    "[Controller]", "[ApiController]", "Microsoft.AspNetCore.Mvc",
                    "System.Web.Mvc", "[Route", "[HttpGet", "[HttpPost",
                    "[HttpPut", "[HttpDelete", "[HttpPatch"
                };
                
                foreach (var pattern in contentPatterns)
                {
                    if (content.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            
            return false;
        }
        catch (Exception ex)
        {
            if (_verbose)
            {
                Console.WriteLine($"Error checking if file is Controller: {filePath}: {ex.Message}");
            }
            return false;
        }
    }
} 