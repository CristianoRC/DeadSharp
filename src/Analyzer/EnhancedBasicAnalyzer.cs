using System.Text.RegularExpressions;

namespace DeadSharp.Analyzer;

/// <summary>
/// Enhanced basic analyzer that can detect advanced patterns without Roslyn
/// This is a fallback when Roslyn analysis fails but enhanced features are requested
/// </summary>
public class EnhancedBasicAnalyzer
{
    private readonly bool _verbose;
    
    public EnhancedBasicAnalyzer(bool verbose = false)
    {
        _verbose = verbose;
    }
    
    /// <summary>
    /// Performs enhanced basic analysis on source code to detect advanced usage patterns
    /// </summary>
    public void AnalyzeAdvancedPatterns(string sourceCode, Dictionary<string, string> allSourceCode, 
        List<FileAnalysisResult> allFileResults)
    {
        if (_verbose)
        {
            Console.WriteLine("Performing enhanced basic analysis (fallback mode)...");
        }
        
        // First do a pre-scan to find all classes directly from source files
        PreScanSourceFiles(allSourceCode, allFileResults);
        
        // Combine all source code for cross-file analysis
        var combinedCode = string.Join("\n", allSourceCode.Values);
        var cleanedCode = RemoveCommentsAndStrings(combinedCode);
        
        // Track classes and methods that should be marked as used
        var usedClasses = new HashSet<string>();
        var usedMethods = new HashSet<string>();
        
        // 1. Factory Pattern Detection
        DetectFactoryPatterns(cleanedCode, usedClasses, usedMethods);
        
        // 2. Interface Implementation Detection
        DetectInterfaceImplementations(cleanedCode, usedClasses);
        
        // 3. Generic Type Parameter Detection
        DetectGenericTypeUsage(cleanedCode, usedClasses);
        
        // 4. Method Return Value Tracking (basic)
        DetectMethodReturnUsage(cleanedCode, usedClasses, usedMethods);
        
        // 5. Lambda and Delegate Usage (basic)
        DetectLambdaUsage(cleanedCode, usedMethods);
        
        // Apply the findings to reduce false positives
        ApplyEnhancedFindings(allFileResults, usedClasses, usedMethods);
        
        if (_verbose)
        {
            Console.WriteLine($"Enhanced basic analysis found {usedClasses.Count} additional used classes and {usedMethods.Count} additional used methods");
        }
    }
    
    private void DetectFactoryPatterns(string code, HashSet<string> usedClasses, HashSet<string> usedMethods)
    {
        // Pattern: factory.Create<ClassName>()
        var factoryPattern = new Regex(@"\.Create<(\w+)>\s*\(", RegexOptions.IgnoreCase);
        var matches = factoryPattern.Matches(code);
        
        foreach (Match match in matches)
        {
            var className = match.Groups[1].Value;
            usedClasses.Add(className);
            
            if (_verbose)
            {
                Console.WriteLine($"  Factory pattern detected: {className}");
            }
        }
        
        // Pattern: Activator.CreateInstance<T>() or typeof(T)
        var activatorPattern = new Regex(@"(?:CreateInstance<(\w+)>|typeof\s*\(\s*(\w+)\s*\))", RegexOptions.IgnoreCase);
        matches = activatorPattern.Matches(code);
        
        foreach (Match match in matches)
        {
            var className = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
            usedClasses.Add(className);
            
            if (_verbose)
            {
                Console.WriteLine($"  Activator/typeof pattern detected: {className}");
            }
        }
    }
    
    private void DetectInterfaceImplementations(string code, HashSet<string> usedClasses)
    {
        // Find interface declarations
        var interfacePattern = new Regex(@"(?:public|internal)?\s*interface\s+(\w+)", RegexOptions.Multiline);
        var interfaces = new HashSet<string>();
        
        foreach (Match match in interfacePattern.Matches(code))
        {
            interfaces.Add(match.Groups[1].Value);
        }
        
        // Find classes that implement these interfaces
        foreach (var interfaceName in interfaces)
        {
            var implementationPattern = new Regex($@"class\s+(\w+)\s*:\s*{interfaceName}", RegexOptions.Multiline);
            var matches = implementationPattern.Matches(code);
            
            foreach (Match match in matches)
            {
                var className = match.Groups[1].Value;
                usedClasses.Add(className);
                
                if (_verbose)
                {
                    Console.WriteLine($"  Interface implementation detected: {className} implements {interfaceName}");
                }
            }
        }
        
        // Also check for interface usage patterns
        foreach (var interfaceName in interfaces)
        {
            var usagePattern = new Regex($@"(?:new\s+(\w+)\s*\(\s*\)|:\s*{interfaceName})", RegexOptions.Multiline);
            var matches = usagePattern.Matches(code);
            
            foreach (Match match in matches)
            {
                if (match.Groups[1].Success)
                {
                    var className = match.Groups[1].Value;
                    usedClasses.Add(className);
                    
                    if (_verbose)
                    {
                        Console.WriteLine($"  Interface usage detected: {className} used via {interfaceName}");
                    }aso específico, o DeadSharp está funcionando bem e encontrando código potencialmente morto, mas ainda requer verificação manual dos resultados para
            // Skip common generic type parameters like T, K, V
            if (className.Length > 1 && !IsCommonGenericParameter(className))
            {
                usedClasses.Add(className);
                
                if (_verbose)
                {
                    Console.WriteLine($"  Generic type usage detected: {className}");
                }
            }
        }
    }
    
    private void DetectMethodReturnUsage(string code, HashSet<string> usedClasses, HashSet<string> usedMethods)
    {
        // Pattern: var x = SomeMethod(); x.DoSomething();
        var assignmentPattern = new Regex(@"var\s+(\w+)\s*=\s*(\w+)\s*\([^)]*\)\s*;", RegexOptions.Multiline);
        var matches = assignmentPattern.Matches(code);
        
        foreach (Match match in matches)
        {
            var variableName = match.Groups[1].Value;
            var methodName = match.Groups[2].Value;
            
            // Look for usage of the variable
            var usagePattern = new Regex($@"{variableName}\.(\w+)", RegexOptions.Multiline);
            var usageMatches = usagePattern.Matches(code);
            
            if (usageMatches.Count > 0)
            {
                usedMethods.Add(methodName);
                
                if (_verbose)
                {
                    Console.WriteLine($"  Method return usage detected: {methodName}");
                }
            }
        }
    }
    
    private void DetectLambdaUsage(string code, HashSet<string> usedMethods)
    {
        // Pattern: .Where(x => x.Method()) or .Select(x => x.Method())
        var lambdaPattern = new Regex(@"\.(?:Where|Select|Any|All|First|FirstOrDefault)\s*\([^)]*=>\s*[^)]*\.(\w+)\s*\([^)]*\)\s*\)", RegexOptions.Multiline);
        var matches = lambdaPattern.Matches(code);
        
        foreach (Match match in matches)
        {
            var methodName = match.Groups[1].Value;
            usedMethods.Add(methodName);
            
            if (_verbose)
            {
                Console.WriteLine($"  Lambda usage detected: {methodName}");
            }
        }
    }
    
    private void ApplyEnhancedFindings(List<FileAnalysisResult> allFileResults, 
        HashSet<string> usedClasses, HashSet<string> usedMethods)
    {
        foreach (var fileResult in allFileResults)
        {
            // Remove classes that were found to be used by enhanced analysis
            var classesToRemove = fileResult.DeadClasses
                .Where(dc => usedClasses.Contains(dc.Name))
                .ToList();
            
            foreach (var classToRemove in classesToRemove)
            {
                fileResult.DeadClasses.Remove(classToRemove);
                fileResult.PotentialDeadClassCount--;
                
                if (_verbose)
                {
                    Console.WriteLine($"  Removed false positive class: {classToRemove.Name}");
                }
            }
            
            // Remove methods that were found to be used by enhanced analysis
            var methodsToRemove = fileResult.DeadMethods
                .Where(dm => usedMethods.Contains(dm.Name))
                .ToList();
            
            foreach (var methodToRemove in methodsToRemove)
            {
                fileResult.DeadMethods.Remove(methodToRemove);
                fileResult.PotentialDeadMethodCount--;
                
                if (_verbose)
                {
                    Console.WriteLine($"  Removed false positive method: {methodToRemove.Name}");
                }
            }
        }
    }
    
    private bool IsCommonGenericParameter(string name)
    {
        var commonParams = new[] { "T", "K", "V", "TKey", "TValue", "TResult", "TSource", "TElement" };
        return commonParams.Contains(name);
    }
    
    /// <summary>
    /// Pre-scans source files to directly identify classes and methods
    /// </summary>
    private void PreScanSourceFiles(Dictionary<string, string> allSourceCode, List<FileAnalysisResult> allFileResults)
    {
        if (_verbose)
        {
            Console.WriteLine("Pre-scanning source files to directly identify classes and methods...");
        }
        
        // Clear existing class/method counts since we'll count them directly
        foreach (var fileResult in allFileResults)
        {
            fileResult.ClassCount = 0;
            fileResult.MethodCount = 0;
        }
        
        // Create a lookup of file paths to FileAnalysisResult
        var fileResultLookup = allFileResults.ToDictionary(fr => fr.FilePath, fr => fr);
        
        // Better regexes for detecting classes, interfaces and methods
        var classRegex = new Regex(@"(?:public|internal|private|protected)?\s*(?:static\s+)?(?:abstract\s+)?(?:sealed\s+)?class\s+(\w+)", RegexOptions.Compiled);
        var interfaceRegex = new Regex(@"(?:public|internal|private|protected)?\s*interface\s+(\w+)", RegexOptions.Compiled);
        var methodRegex = new Regex(@"(?:public|internal|private|protected)?\s*(?:static\s+)?(?:async\s+)?(?:virtual\s+)?(?:override\s+)?(?:\w+(?:<[^>]+>)?\s+)?(\w+)\s*\([^)]*\)", RegexOptions.Compiled);
        
        foreach (var kvp in allSourceCode)
        {
            var filePath = kvp.Key;
            var sourceCode = kvp.Value;
            
            // Skip if we don't have this file in our results (should never happen)
            if (!fileResultLookup.ContainsKey(filePath))
                continue;
            
            var fileResult = fileResultLookup[filePath];
            
            // Count classes
            var classMatches = classRegex.Matches(sourceCode);
            fileResult.ClassCount += classMatches.Count;
            
            if (_verbose && classMatches.Count > 0)
            {
                Console.WriteLine($"  Found {classMatches.Count} classes in {Path.GetFileName(filePath)}:");
                foreach (Match match in classMatches)
                {
                    Console.WriteLine($"    - {match.Groups[1].Value}");
                }
            }
            
            // Count interfaces
            var interfaceMatches = interfaceRegex.Matches(sourceCode);
            fileResult.ClassCount += interfaceMatches.Count; // Count interfaces as classes
            
            if (_verbose && interfaceMatches.Count > 0)
            {
                Console.WriteLine($"  Found {interfaceMatches.Count} interfaces in {Path.GetFileName(filePath)}:");
                foreach (Match match in interfaceMatches)
                {
                    Console.WriteLine($"    - {match.Groups[1].Value}");
                }
            }
            
            // Count methods
            var methodMatches = methodRegex.Matches(sourceCode);
            fileResult.MethodCount = methodMatches.Count;
            
            if (_verbose && methodMatches.Count > 0)
            {
                Console.WriteLine($"  Found {methodMatches.Count} methods in {Path.GetFileName(filePath)}");
            }
        }
    }
    
    private string RemoveCommentsAndStrings(string sourceCode)
    {
        // Remove single-line comments
        sourceCode = Regex.Replace(sourceCode, @"//.*$", "", RegexOptions.Multiline);
        
        // Remove multi-line comments
        sourceCode = Regex.Replace(sourceCode, @"/\*.*?\*/", "", RegexOptions.Singleline);
        
        // Remove string literals
        sourceCode = Regex.Replace(sourceCode, @"""(?:[^""\\]|\\.)*""", "\"\"", RegexOptions.Singleline);
        sourceCode = Regex.Replace(sourceCode, @"'(?:[^'\\]|\\.)*'", "''", RegexOptions.Singleline);
        
        // Remove verbatim strings
        sourceCode = Regex.Replace(sourceCode, @"@""(?:[^""]|"""")*""", "\"\"", RegexOptions.Singleline);
        
        return sourceCode;
    }
} 