using System.Text.RegularExpressions;

namespace DeadSharp.Analyzer;

/// <summary>
/// Parser for Visual Studio solution files (.sln)
/// </summary>
public static class SolutionParser
{
    private static readonly Regex ProjectRegex = new(
        @"Project\(""\{[^}]+\}""\)\s*=\s*""([^""]+)""\s*,\s*""([^""]+)""\s*,\s*""\{[^}]+\}""",
        RegexOptions.Compiled | RegexOptions.Multiline);

    /// <summary>
    /// Extracts project file paths from a solution file
    /// </summary>
    /// <param name="solutionPath">Path to the .sln file</param>
    /// <returns>List of project file paths</returns>
    public static async Task<List<string>> ExtractProjectPathsAsync(string solutionPath)
    {
        var projectPaths = new List<string>();
        
        try
        {
            var solutionContent = await File.ReadAllTextAsync(solutionPath);
            var solutionDirectory = Path.GetDirectoryName(solutionPath) ?? string.Empty;
            
            var matches = ProjectRegex.Matches(solutionContent);
            
            foreach (Match match in matches)
            {
                if (match.Groups.Count >= 3)
                {
                    var projectName = match.Groups[1].Value;
                    var relativePath = match.Groups[2].Value;
                    
                    // Skip solution folders and non-C# projects
                    if (relativePath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                    {
                        // Convert relative path to absolute path
                        var absolutePath = Path.Combine(solutionDirectory, relativePath);
                        absolutePath = Path.GetFullPath(absolutePath);
                        
                        if (File.Exists(absolutePath))
                        {
                            projectPaths.Add(absolutePath);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing solution file {solutionPath}: {ex.Message}");
        }
        
        return projectPaths;
    }
    
    /// <summary>
    /// Gets basic information about a solution
    /// </summary>
    /// <param name="solutionPath">Path to the .sln file</param>
    /// <returns>Solution information</returns>
    public static async Task<SolutionInfo> GetSolutionInfoAsync(string solutionPath)
    {
        var info = new SolutionInfo
        {
            SolutionPath = solutionPath,
            SolutionName = Path.GetFileNameWithoutExtension(solutionPath)
        };
        
        try
        {
            var solutionContent = await File.ReadAllTextAsync(solutionPath);
            
            // Extract Visual Studio version
            var versionMatch = Regex.Match(solutionContent, @"VisualStudioVersion\s*=\s*([^\r\n]+)");
            if (versionMatch.Success)
            {
                info.VisualStudioVersion = versionMatch.Groups[1].Value.Trim();
            }
            
            // Extract minimum Visual Studio version
            var minVersionMatch = Regex.Match(solutionContent, @"MinimumVisualStudioVersion\s*=\s*([^\r\n]+)");
            if (minVersionMatch.Success)
            {
                info.MinimumVisualStudioVersion = minVersionMatch.Groups[1].Value.Trim();
            }
            
            // Count projects
            info.ProjectPaths = await ExtractProjectPathsAsync(solutionPath);
            info.ProjectCount = info.ProjectPaths.Count;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting solution info for {solutionPath}: {ex.Message}");
        }
        
        return info;
    }
}

/// <summary>
/// Information about a Visual Studio solution
/// </summary>
public class SolutionInfo
{
    /// <summary>
    /// Path to the solution file
    /// </summary>
    public string SolutionPath { get; set; } = string.Empty;
    
    /// <summary>
    /// Name of the solution
    /// </summary>
    public string SolutionName { get; set; } = string.Empty;
    
    /// <summary>
    /// Visual Studio version
    /// </summary>
    public string? VisualStudioVersion { get; set; }
    
    /// <summary>
    /// Minimum Visual Studio version required
    /// </summary>
    public string? MinimumVisualStudioVersion { get; set; }
    
    /// <summary>
    /// Number of projects in the solution
    /// </summary>
    public int ProjectCount { get; set; }
    
    /// <summary>
    /// Paths to all project files in the solution
    /// </summary>
    public List<string> ProjectPaths { get; set; } = new();
} 