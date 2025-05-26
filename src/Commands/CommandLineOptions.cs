using System.CommandLine;

namespace DeadSharp.Commands;

/// <summary>
/// Configures and provides command line options for the DeadSharp tool
/// </summary>
public static class CommandLineOptions
{
    /// <summary>
    /// Option for specifying the project path to analyze
    /// </summary>
    public static Option<string> ProjectPath { get; } = CreateProjectPathOption();
    
    /// <summary>
    /// Option for enabling verbose output
    /// </summary>
    public static Option<bool> Verbose { get; } = CreateVerboseOption();
    
    /// <summary>
    /// Option for ignoring test projects during analysis
    /// </summary>
    public static Option<bool> IgnoreTests { get; } = CreateIgnoreTestsOption();
    
    /// <summary>
    /// Option for ignoring database migrations during analysis
    /// </summary>
    public static Option<bool> IgnoreMigrations { get; } = CreateIgnoreMigrationsOption();
    
    /// <summary>
    /// Option for ignoring Azure Functions during analysis
    /// </summary>
    public static Option<bool> IgnoreAzureFunctions { get; } = CreateIgnoreAzureFunctionsOption();
    
    /// <summary>
    /// Option for ignoring Controllers during analysis
    /// </summary>
    public static Option<bool> IgnoreControllers { get; } = CreateIgnoreControllersOption();
    
    /// <summary>
    /// Option for enabling enhanced dependency injection detection
    /// </summary>
    public static Option<bool> EnhancedDiDetection { get; } = CreateEnhancedDiDetectionOption();
    
    /// <summary>
    /// Option for enabling enhanced data flow analysis (advanced semantic analysis)
    /// </summary>
    public static Option<bool> EnhancedDataFlow { get; } = CreateEnhancedDataFlowOption();
    
    /// <summary>
    /// Option for specifying the output path for results
    /// </summary>
    public static Option<string> OutputPath { get; } = CreateOutputPathOption();
    
    /// <summary>
    /// Option for specifying the output format (JSON or TXT)
    /// </summary>
    public static Option<string> OutputFormat { get; } = CreateOutputFormatOption();
    
    /// <summary>
    /// Creates and configures the root command with all options
    /// </summary>
    /// <returns>Configured root command</returns>
    public static RootCommand CreateRootCommand()
    {
        var rootCommand = new RootCommand("DeadSharp: A tool for analyzing C# projects to identify dead code");
        
        rootCommand.AddOption(ProjectPath);
        rootCommand.AddOption(Verbose);
        rootCommand.AddOption(IgnoreTests);
        rootCommand.AddOption(IgnoreMigrations);
        rootCommand.AddOption(IgnoreAzureFunctions);
        rootCommand.AddOption(IgnoreControllers);
        rootCommand.AddOption(EnhancedDiDetection);
        rootCommand.AddOption(EnhancedDataFlow);
        rootCommand.AddOption(OutputPath);
        rootCommand.AddOption(OutputFormat);
        
        return rootCommand;
    }
    
    private static Option<string> CreateProjectPathOption()
    {
        var option = new Option<string>(
            name: "--path",
            description: "Path to the project or solution to analyze")
        {
            IsRequired = true
        };
        
        option.AddAlias("-p");
        
        // Add validation
        option.AddValidator(result =>
        {
            var path = result.GetValueForOption(option);
            if (string.IsNullOrWhiteSpace(path))
            {
                result.ErrorMessage = "Path cannot be empty";
                return;
            }
            
            if (!Directory.Exists(path) && !File.Exists(path))
            {
                result.ErrorMessage = $"The path '{path}' does not exist";
                return;
            }
            
            // Check if it's a valid C# project path
            if (Directory.Exists(path))
            {
                var hasProjectFiles = Directory.GetFiles(path, "*.csproj", SearchOption.AllDirectories).Any() ||
                                     Directory.GetFiles(path, "*.sln", SearchOption.AllDirectories).Any();
                
                if (!hasProjectFiles)
                {
                    result.ErrorMessage = "The specified directory does not contain any .csproj or .sln files";
                }
            }
            else if (File.Exists(path))
            {
                var extension = Path.GetExtension(path).ToLowerInvariant();
                if (extension != ".csproj" && extension != ".sln")
                {
                    result.ErrorMessage = "The specified file must be a .csproj or .sln file";
                }
            }
        });
        
        return option;
    }
    
    private static Option<bool> CreateVerboseOption()
    {
        var option = new Option<bool>(
            name: "--verbose",
            description: "Enable verbose output for detailed analysis information");
        
        option.AddAlias("-v");
        
        return option;
    }
    
    private static Option<bool> CreateIgnoreTestsOption()
    {
        var option = new Option<bool>(
            name: "--ignore-tests",
            description: "Ignore test projects during analysis");
        
        option.AddAlias("-i");
        
        return option;
    }
    
    private static Option<bool> CreateIgnoreMigrationsOption()
    {
        var option = new Option<bool>(
            name: "--ignore-migrations",
            description: "Ignore database migration files during analysis");
        
        option.AddAlias("-im");
        
        return option;
    }
    
    private static Option<bool> CreateIgnoreAzureFunctionsOption()
    {
        var option = new Option<bool>(
            name: "--ignore-azure-functions",
            description: "Ignore Azure Function files during analysis");
        
        option.AddAlias("-iaf");
        
        return option;
    }
    
    private static Option<bool> CreateIgnoreControllersOption()
    {
        var option = new Option<bool>(
            name: "--ignore-controllers",
            description: "Ignore Controller files during analysis");
        
        option.AddAlias("-ic");
        
        return option;
    }
    
    private static Option<bool> CreateEnhancedDiDetectionOption()
    {
        var option = new Option<bool>(
            name: "--enhanced-di-detection",
            description: "Enable enhanced dependency injection detection to better identify classes used only in DI containers");
        
        option.AddAlias("-ed");
        
        return option;
    }
    
    private static Option<bool> CreateEnhancedDataFlowOption()
    {
        var option = new Option<bool>(
            name: "--enhanced-dataflow",
            description: "Enable enhanced data flow analysis using deep semantic analysis to detect complex usage patterns through indirect references, factory patterns, and control flow tracking");
        
        option.AddAlias("-edf");
        
        return option;
    }
    
    private static Option<string> CreateOutputPathOption()
    {
        var option = new Option<string>(
            name: "--output",
            description: "Path to save the analysis results in JSON format")
        {
            IsRequired = false
        };
        
        option.AddAlias("-o");
        
        return option;
    }
    
    private static Option<string> CreateOutputFormatOption()
    {
        var option = new Option<string>(
            name: "--format",
            description: "Formato de saída dos resultados (JSON ou TXT). Padrão é console se nenhum formato for especificado.",
            getDefaultValue: () => "console")
        {
            IsRequired = false
        };
        
        option.AddAlias("-f");
        
        // Adicionando validação para aceitar apenas JSON ou TXT
        option.AddValidator(result =>
        {
            var format = result.GetValueForOption(option);
            if (!string.IsNullOrEmpty(format) && 
                format.ToUpperInvariant() != "JSON" && 
                format.ToUpperInvariant() != "TXT" &&
                format.ToUpperInvariant() != "CONSOLE")
            {
                result.ErrorMessage = "O formato deve ser JSON, TXT ou CONSOLE";
            }
        });
        
        return option;
    }
} 