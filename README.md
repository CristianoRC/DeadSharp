# DeadSharp

DeadSharp is a command-line tool for analyzing C# projects and identifying dead code.

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

# Combine multiple ignore options
deadsharp -p /path/to/your/project -v --ignore-tests --ignore-migrations --ignore-controllers

# Use short aliases
deadsharp -p /path/to/your/project -v -i -im -iaf -ic
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

#### Combining Ignore Options

You can combine multiple ignore options to fine-tune your analysis:

```bash
# Ignore all framework-specific files
deadsharp -p /path/to/project --ignore-tests --ignore-migrations --ignore-azure-functions --ignore-controllers

# Using short aliases
deadsharp -p /path/to/project -i -im -iaf -ic
```

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
    ├── CodeAnalyzer.cs          # Main code analyzer
    └── AnalysisResult.cs        # Analysis result models
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

