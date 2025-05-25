namespace DeadSharp.Analyzer;

/// <summary>
/// Contains the overall results of a code analysis operation
/// </summary>
public class AnalysisResult
{
    /// <summary>
    /// Path to the project or solution that was analyzed
    /// </summary>
    public string ProjectPath { get; set; } = string.Empty;
    
    /// <summary>
    /// When the analysis started
    /// </summary>
    public DateTime StartTime { get; set; }
    
    /// <summary>
    /// When the analysis completed
    /// </summary>
    public DateTime EndTime { get; set; }
    
    /// <summary>
    /// Whether the analysis completed successfully
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// Error message if the analysis failed
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// Analysis results for each project
    /// </summary>
    public List<ProjectAnalysisResult> ProjectResults { get; set; } = new();
    
    /// <summary>
    /// Total duration of the analysis
    /// </summary>
    public TimeSpan Duration => EndTime - StartTime;
    
    /// <summary>
    /// Total number of source files analyzed
    /// </summary>
    public int TotalSourceFiles => ProjectResults.Sum(p => p.SourceFileCount);
    
    /// <summary>
    /// Total number of methods analyzed
    /// </summary>
    public int TotalMethods => ProjectResults.Sum(p => p.TotalMethods);
    
    /// <summary>
    /// Total number of potentially dead methods found
    /// </summary>
    public int TotalPotentialDeadMethods => ProjectResults.Sum(p => p.PotentialDeadMethods);
    
    /// <summary>
    /// Total number of classes analyzed
    /// </summary>
    public int TotalClasses => ProjectResults.Sum(p => p.TotalClasses);
    
    /// <summary>
    /// Total number of potentially dead classes found
    /// </summary>
    public int TotalPotentialDeadClasses => ProjectResults.Sum(p => p.PotentialDeadClasses);
    
    /// <summary>
    /// Percentage of methods that are potentially dead
    /// </summary>
    public double DeadMethodPercentage => TotalMethods > 0 ? (double)TotalPotentialDeadMethods / TotalMethods * 100 : 0;
    
    /// <summary>
    /// Percentage of classes that are potentially dead
    /// </summary>
    public double DeadClassPercentage => TotalClasses > 0 ? (double)TotalPotentialDeadClasses / TotalClasses * 100 : 0;
}

/// <summary>
/// Contains analysis results for a specific project
/// </summary>
public class ProjectAnalysisResult
{
    /// <summary>
    /// Path to the project file
    /// </summary>
    public string ProjectPath { get; set; } = string.Empty;
    
    /// <summary>
    /// Number of source files in the project
    /// </summary>
    public int SourceFileCount { get; set; }
    
    /// <summary>
    /// Analysis results for each source file
    /// </summary>
    public List<FileAnalysisResult> FileResults { get; set; } = new();
    
    /// <summary>
    /// Total number of methods in the project
    /// </summary>
    public int TotalMethods { get; set; }
    
    /// <summary>
    /// Number of potentially dead methods in the project
    /// </summary>
    public int PotentialDeadMethods { get; set; }
    
    /// <summary>
    /// Total number of classes in the project
    /// </summary>
    public int TotalClasses { get; set; }
    
    /// <summary>
    /// Number of potentially dead classes in the project
    /// </summary>
    public int PotentialDeadClasses { get; set; }
}

/// <summary>
/// Contains analysis results for a specific source file
/// </summary>
public class FileAnalysisResult
{
    /// <summary>
    /// Full path to the file
    /// </summary>
    public string FilePath { get; set; } = string.Empty;
    
    /// <summary>
    /// Relative path from the current directory
    /// </summary>
    public string RelativePath { get; set; } = string.Empty;
    
    /// <summary>
    /// Number of classes in the file
    /// </summary>
    public int ClassCount { get; set; }
    
    /// <summary>
    /// Number of methods in the file
    /// </summary>
    public int MethodCount { get; set; }
    
    /// <summary>
    /// Number of potentially dead methods in the file
    /// </summary>
    public int PotentialDeadMethodCount { get; set; }
    
    /// <summary>
    /// Number of potentially dead classes in the file
    /// </summary>
    public int PotentialDeadClassCount { get; set; }
    
    /// <summary>
    /// Error message if analysis failed
    /// </summary>
    public string? Error { get; set; }
    
    /// <summary>
    /// Details about potentially dead methods
    /// </summary>
    public List<DeadCodeItem> DeadMethods { get; set; } = new();
    
    /// <summary>
    /// Details about potentially dead classes
    /// </summary>
    public List<DeadCodeItem> DeadClasses { get; set; } = new();
}

/// <summary>
/// Represents a potentially dead code item (method or class)
/// </summary>
public class DeadCodeItem
{
    /// <summary>
    /// Name of the code item
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Type of the code item (method, class, etc.)
    /// </summary>
    public string Type { get; set; } = string.Empty;
    
    /// <summary>
    /// Line number where the item is defined
    /// </summary>
    public int LineNumber { get; set; }
    
    /// <summary>
    /// Column number where the item is defined
    /// </summary>
    public int ColumnNumber { get; set; }
    
    /// <summary>
    /// Confidence level that this item is actually dead code (0-100)
    /// </summary>
    public int ConfidencePercentage { get; set; }
    
    /// <summary>
    /// Reason why this item is considered dead code
    /// </summary>
    public string Reason { get; set; } = string.Empty;
} 