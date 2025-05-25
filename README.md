# DeadSharp

DeadSharp is a command-line tool for analyzing C# projects and identifying dead code.



## 🆕 What's New

**Enhanced Data Flow Analysis** - Advanced semantic analysis capabilities! The new `--enhanced-dataflow` option provides sophisticated pattern detection that significantly reduces false positives by tracking:

- 🏭 **Factory patterns** - Detects classes used via `factory.Create<T>()`
- 🔄 **Data flow tracking** - Follows variable usage through complex control flows  
- 🎯 **Interface implementations** - Automatically marks implementations when interfaces are used
- 🧩 **Generic type parameters** - Detects usage in generic constraints and type arguments
- 🔗 **Lambda expressions** - Deep analysis of delegate and lambda usage

**Result**: Much more accurate dead code detection with fewer false positives!

## Installation

### From NuGet (when published)
```bash
dotnet tool install --global DeadSharp
```

### From Source Code
1. Clone the repository
2. Build the project
   ```bash
   cd src
   dotnet pack
   dotnet tool install --global --add-source ./nupkg DeadSharp
   ```

## Usage

```bash
# Basic usage
deadsharp --path /path/to/your/project

# Or with short parameter
deadsharp -p /path/to/your/project

# Enable verbose output
deadsharp -p /path/to/your/project -v

# Ignore test projects during analysis
deadsharp -p /path/to/your/project --ignore-tests

# Ignore database migrations during analysis
deadsharp -p /path/to/your/project --ignore-migrations

# Ignore Azure Functions during analysis
deadsharp -p /path/to/your/project --ignore-azure-functions

# Ignore Controllers during analysis
deadsharp -p /path/to/your/project --ignore-controllers

# Enable enhanced dependency injection detection
deadsharp -p /path/to/your/project --enhanced-di-detection

# Enable enhanced data flow analysis (advanced semantic analysis)
deadsharp -p /path/to/your/project --enhanced-dataflow

# Save results to a JSON file
deadsharp -p /path/to/your/project --output results.json

# Combine multiple options
deadsharp -p /path/to/your/project -v --ignore-tests --ignore-migrations --ignore-controllers --enhanced-di-detection --enhanced-dataflow --output results.json

# Use short aliases
deadsharp -p /path/to/your/project -v -i -im -iaf -ic -ed -edf -o results.json
```

### Important Note About Large Projects

For large projects, it's recommended to use the `--output` option to save the results to a JSON file. This is because:

1. Large projects may generate a lot of output that might not fit in the console buffer
2. The JSON output can be used for further analysis or integration with other tools
3. You can review the results at your own pace without time constraints
4. The results can be shared with team members or stored for future reference

Example for large projects:
```bash
deadsharp -p /path/to/large/project --output analysis-results.json
```

### Supported Input Types

- **Directories**: Analyzes all .csproj and .sln files found
- **.sln files**: Analyzes all projects in the solution
- **.csproj files**: Analyzes the specific project

### Advanced Options

#### Ignore Test Projects (`--ignore-tests` / `-i`)

By default, the tool analyzes all projects found, including test projects. This can generate many false positives, as test methods are executed by testing frameworks and are not "called" directly in the code.

Use the `--ignore-tests` option to automatically filter out test projects:

```bash
deadsharp -p /path/to/project --ignore-tests
```

The tool detects test projects based on:
- **Naming patterns**: projects containing "test", "tests", "unittest", "spec", etc.
- **Dependencies**: projects that reference packages like xUnit, NUnit, MSTest, Moq, FluentAssertions, etc.

**Example result:**
- Without `--ignore-tests`: 89 potentially dead methods
- With `--ignore-tests`: 35 potentially dead methods (54 false positives removed)

#### Ignore Database Migrations (`--ignore-migrations` / `-im`)

Database migration files often contain methods that appear unused but are called by Entity Framework or other ORM frameworks during database updates.

Use the `--ignore-migrations` option to skip migration files during analysis:

```bash
deadsharp -p /path/to/project --ignore-migrations
```

The tool detects migration files based on:
- **Filename patterns**: containing "Migration", "migrations", "_CreateTable", "_AddColumn", etc.
- **Directory patterns**: located in "Migrations" or "migrations" directories
- **Content patterns**: containing "MigrationBuilder", "CreateTable", "DropTable", "EntityFramework", etc.

#### Ignore Azure Functions (`--ignore-azure-functions` / `-iaf`)

Azure Function files contain methods that are invoked by the Azure Functions runtime and may appear as dead code to static analysis.

Use the `--ignore-azure-functions` option to skip Azure Function files during analysis:

```bash
deadsharp -p /path/to/project --ignore-azure-functions
```

The tool detects Azure Function files based on:
- **Filename patterns**: containing "Function", "functions", "AzureFunction", etc.
- **Content patterns**: containing "[FunctionName", "Microsoft.Azure.WebJobs", trigger attributes like "HttpTrigger", "TimerTrigger", etc.

#### Ignore Controllers (`--ignore-controllers` / `-ic`)

Controller files in web applications contain action methods that are invoked by the web framework through routing and may appear unused to static analysis.

Use the `--ignore-controllers` option to skip Controller files during analysis:

```bash
deadsharp -p /path/to/project --ignore-controllers
```

The tool detects Controller files based on:
- **Filename patterns**: ending with "Controller.cs"
- **Content patterns**: inheriting from "Controller", "ControllerBase", or "ApiController", containing MVC attributes like "[ApiController]", "[HttpGet]", etc.

#### Enhanced Dependency Injection Detection (`--enhanced-di-detection` / `-ed`)

One of the most common false positives in dead code analysis occurs when classes are only used through dependency injection (DI) containers. These classes may appear unused because they're only referenced in DI registration code and injected via interfaces.

Use the `--enhanced-di-detection` option to enable advanced detection of dependency injection patterns:

```bash
deadsharp -p /path/to/project --enhanced-di-detection
```

This feature detects classes used in various DI scenarios:

**DI Container Registration Patterns:**
- `services.AddScoped<IService, Service>()`
- `services.AddTransient<Service>()`
- `services.AddSingleton(typeof(Service))`
- `container.Register<IService, Service>()`
- `kernel.Bind<IService>().To<Service>()`
- `For<IService>().Use<Service>()` (StructureMap)
- `services.Configure<OptionsClass>()`

**Service Resolution Patterns:**
- `serviceProvider.GetService<Service>()`
- `serviceProvider.GetRequiredService<Service>()`
- `container.Resolve<Service>()`

**Constructor Injection:**
- Classes injected as constructor parameters
- Automatic detection of DI constructor patterns

**Attribute-based Injection:**
- `[FromServices]` parameters in controller actions
- `[Inject]` and `[Dependency]` attributes
- Property injection patterns

**Generic Type Constraints:**
- `where T : IService` constraints
- `typeof(Service)` expressions

**Example scenario:**
```csharp
// This class might appear as "dead code" without enhanced DI detection
public class EmailService : IEmailService
{
    public void SendEmail(string to, string subject, string body) { /* implementation */ }
}

// But it's registered in DI container
services.AddScoped<IEmailService, EmailService>();

// And injected via constructor
public class UserController : ControllerBase
{
    private readonly IEmailService _emailService;
    
    public UserController(IEmailService emailService) // EmailService is used here!
    {
        _emailService = emailService;
    }
}
```

With `--enhanced-di-detection`, the tool will correctly identify that `EmailService` is used through the DI container and won't mark it as dead code.

#### Enhanced Data Flow Analysis (`--enhanced-dataflow` / `-edf`) 🆕

**NEW FEATURE**: Advanced semantic data flow analysis that significantly reduces false positives by tracking complex usage patterns that basic static analysis might miss. This feature uses deep code analysis to understand how classes and methods are used through indirect references, complex control flows, and sophisticated programming patterns.

Use the `--enhanced-dataflow` option to enable advanced semantic analysis:

```bash
deadsharp -p /path/to/project --enhanced-dataflow
```

This feature detects usage through sophisticated patterns:

**Factory Pattern Detection:**
```csharp
// Without enhanced data flow: FactoryCreatedService appears unused
public class ServiceFactory
{
    public T Create<T>() where T : new() => new T();
}

public class FactoryCreatedService
{
    public void Execute() { /* implementation */ }
}

// Usage that enhanced data flow detects:
var factory = new ServiceFactory();
var service = factory.Create<FactoryCreatedService>(); // ✅ Detected!
service.Execute();
```

**Method Return Value Tracking:**
```csharp
// Enhanced data flow tracks usage through return values
private static IService GetService()
{
    return new MyService(); // ✅ MyService marked as used
}

var service = GetService(); // ✅ Tracks this assignment
service.DoWork(); // ✅ And subsequent usage
```

**Interface Implementation Detection:**
```csharp
// Enhanced data flow automatically marks implementations as used
public interface IService { void DoWork(); }
public class MyService : IService { /* implementation */ }

// When interface is used, implementation is marked as used
IService service = new MyService(); // ✅ Both interface and implementation detected
```

**Lambda and Delegate Usage:**
```csharp
// Detects usage within lambda expressions and delegates
services.Where(s => s.IsActive()).Select(s => s.Process()); // ✅ IsActive and Process detected
```

**Conditional Flow Analysis:**
```csharp
// Tracks usage through complex control flows
if (condition)
{
    var service = GetSpecialService(); // ✅ Detected even in conditional blocks
    service.Execute();
}
```

**Generic Type Parameter Detection:**
```csharp
// Detects classes used as generic type parameters
public class Repository<T> where T : class { }
var repo = new Repository<MyEntity>(); // ✅ MyEntity marked as used
```

**Comparison with Basic Analysis:**

| Scenario | Basic Analysis | Enhanced Data Flow |
|----------|----------------|-------------------|
| `factory.Create<Service>()` | ❌ Service appears unused | ✅ Service detected as used |
| Interface implementations | ❌ Implementation appears unused | ✅ Implementation detected via interface |
| Method return tracking | ❌ Limited tracking | ✅ Full data flow tracking |
| Lambda expressions | ❌ Basic pattern matching | ✅ Deep lambda analysis |
| Generic constraints | ❌ Often missed | ✅ Comprehensive detection |

**Performance Note:** Enhanced data flow analysis requires deep semantic analysis using the Roslyn compiler platform and may take longer on large projects, but provides significantly more accurate results by understanding the actual code semantics rather than just pattern matching.

## Features

- ✅ Analysis of C# projects to identify unused code
- ✅ Works with project files (.csproj) and solution files (.sln)
- ✅ Detailed reports of dead code locations
- ✅ Input validation with clear error messages
- ✅ Verbose mode for detailed analysis
- ✅ Smart ignore options to reduce false positives:
  - ✅ Ignore test projects (`--ignore-tests`)
  - ✅ Ignore database migrations (`--ignore-migrations`)
  - ✅ Ignore Azure Functions (`--ignore-azure-functions`)
  - ✅ Ignore Controllers (`--ignore-controllers`)
- ✅ Enhanced dependency injection detection (`--enhanced-di-detection`)
- ✅ **NEW**: Enhanced data flow analysis (`--enhanced-dataflow`) - Advanced semantic analysis capabilities
- ✅ Short aliases for all options
- ✅ Modular and extensible architecture
- ✅ Both Roslyn-based semantic analysis and fallback regex analysis

## Project Structure

```
src/
├── Program.cs                    # Main entry point
├── Commands/
│   ├── CommandLineOptions.cs    # Command line options configuration
│   └── AnalyzeCommand.cs        # Analysis command logic
└── Analyzer/
    ├── CodeAnalyzer.cs              # Main code analyzer
    ├── RoslynAnalyzer.cs            # Roslyn-based semantic analysis
    ├── EnhancedDataFlowAnalyzer.cs  # Advanced data flow analysis (NEW)
    └── AnalysisResult.cs            # Analysis result models
```

## Development

### Build
```bash
cd src
dotnet build
```

### Test Locally
```bash
cd src
dotnet run -- --path /path/to/project --verbose
```

### Package
```bash
cd src
dotnet pack
```

## License

See the [LICENSE](LICENSE) file for details.

## Contributing

Pull requests are welcome! If you'd like to contribute, please fork the repo and submit a PR. Bug reports and feature requests are also highly appreciated.

## ⚠️ Disclaimer

**Early Stage Project**: DeadSharp is in a very early stage of development. The tool is constantly evolving and may exhibit unexpected behaviors.

**Analysis Accuracy**:
- **False Positives**: The tool may identify code as "dead" that is actually being used, especially in complex patterns like dependency injection, reflection, dynamic loading, and framework conventions. Always manually review results before removing code.
- **False Negatives**: There is also the possibility of false negatives, where genuinely unused code is not detected by the tool, particularly in cases of indirectly referenced code or through patterns not recognized by the analyzer.

**Recommended Use**: Use DeadSharp as an auxiliary tool to identify potential candidates for refactoring or removal, always with manual verification of the results.

