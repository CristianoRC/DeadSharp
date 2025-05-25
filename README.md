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

# Combine options
deadsharp -p /path/to/your/project -v --ignore-tests
```

### Supported Input Types

- **Directories**: Analyzes all .csproj and .sln files found
- **.sln files**: Analyzes all projects in the solution
- **.csproj files**: Analyzes the specific project

### Advanced Options

#### Ignore Test Projects (`--ignore-tests`)

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

## Features

- ✅ Analysis of C# projects to identify unused code
- ✅ Works with project files (.csproj) and solution files (.sln)
- ✅ Detailed reports of dead code locations
- ✅ Input validation with clear error messages
- ✅ Verbose mode for detailed analysis
- ✅ Option to ignore test projects (reduces false positives)
- ✅ Modular and extensible architecture

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

