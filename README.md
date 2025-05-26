# DeadSharp


[![Build and Publish to NuGet](https://github.com/CristianoRC/DeadSharp/actions/workflows/publish.yml/badge.svg)](https://github.com/CristianoRC/DeadSharp/actions/workflows/publish.yml) ![NuGet Version](https://img.shields.io/nuget/v/DeadSharp?logo=nuget&color=blue)


DeadSharp is a command-line tool for analyzing C# projects and identifying dead code.


## Installation

### From NuGet
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

## Updating to the Latest Version

### From NuGet
```bash
dotnet tool update --global DeadSharp
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
deadsharp -p /path/to/your/project --output results.json --format JSON

# Save results to a TXT file (for large reports)
deadsharp -p /path/to/your/project --output results.txt --format TXT

# Combine multiple options
deadsharp -p /path/to/your/project -v --ignore-tests --ignore-migrations --ignore-controllers --enhanced-di-detection --enhanced-dataflow --output results.json --format JSON

# Use short aliases
deadsharp -p /path/to/your/project -v -i -im -iaf -ic -ed -edf -o results.json -f JSON
```

### Output Formats

The tool supports three output formats:

1. **Console (default)**: Displays the results directly in the console.

2. **JSON**: Saves the results in JSON format, ideal for further processing or integration with other tools.
   ```bash
   deadsharp -p /path/to/your/project --output results.json --format JSON
   # or short form
   deadsharp -p /path/to/your/project -o results.json -f JSON
   ```

3. **TXT**: Saves the results in plain text format, ideal for large reports that need to be read by humans.
   ```bash
   deadsharp -p /path/to/your/project --output results.txt --format TXT
   # or short form
   deadsharp -p /path/to/your/project -o results.txt -f TXT
   ```

### Important Note About Large Projects

For large projects, it's recommended to use the `--output` option with a specific format to save the results. This is recommended because:

1. Large projects may generate a lot of output that might not fit in the console buffer
2. JSON output can be used for further analysis or integration with other tools
3. TXT output is easier to read for extensive reports
4. Results can be shared with team members or stored for future reference

Example for large projects:
```bash
deadsharp -p /path/to/large/project --output analysis-results.txt --format TXT
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

#### Ignore Database Migrations (`--ignore-migrations` / `-im`)

Database migration files often contain methods that appear unused but are called by Entity Framework or other ORM frameworks during database updates.

Use the `--ignore-migrations` option to skip migration files during analysis:

```bash
deadsharp -p /path/to/project --ignore-migrations
```

#### Ignore Azure Functions (`--ignore-azure-functions` / `-iaf`)

Azure Function files contain methods that are invoked by the Azure Functions runtime and may appear as dead code to static analysis.

Use the `--ignore-azure-functions` option to skip Azure Function files during analysis:

```bash
deadsharp -p /path/to/project --ignore-azure-functions
```

#### Ignore Controllers (`--ignore-controllers` / `-ic`)

Controller files in web applications contain action methods that are invoked by the web framework through routing and may appear unused to static analysis.

Use the `--ignore-controllers` option to skip Controller files during analysis:

```bash
deadsharp -p /path/to/project --ignore-controllers
```

#### Enhanced Dependency Injection Detection (`--enhanced-di-detection` / `-ed`)

One of the most common false positives in dead code analysis occurs when classes are only used through dependency injection (DI) containers. These classes may appear unused because they're only referenced in DI registration code and injected via interfaces.

Use the `--enhanced-di-detection` option to enable advanced detection of dependency injection patterns:

```bash
deadsharp -p /path/to/project --enhanced-di-detection
```

#### Enhanced Data Flow Analysis (`--enhanced-dataflow` / `-edf`)

Advanced semantic data flow analysis that significantly reduces false positives by tracking complex usage patterns that basic static analysis might miss. This feature uses deep code analysis to understand how classes and methods are used through indirect references, complex control flows, and sophisticated programming patterns.

Use the `--enhanced-dataflow` option to enable advanced semantic analysis:

```bash
deadsharp -p /path/to/project --enhanced-dataflow
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
- ✅ Enhanced dependency injection detection (`--enhanced-di-detection`)
- ✅ Enhanced data flow analysis (`--enhanced-dataflow`)
- ✅ Multiple output formats (Console, JSON, TXT) (`--format`)
- ✅ Short aliases for all options
- ✅ Modular and extensible architecture
- ✅ Both Roslyn-based semantic analysis and fallback regex analysis

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

