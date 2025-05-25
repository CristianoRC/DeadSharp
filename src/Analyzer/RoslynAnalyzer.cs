using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Build.Locator;

namespace DeadSharp.Analyzer;

/// <summary>
/// Advanced code analyzer using Roslyn for semantic analysis
/// </summary>
public class RoslynAnalyzer
{
    private readonly bool _verbose;
    private readonly bool _ignoreTests;
    private readonly bool _ignoreMigrations;
    private readonly bool _ignoreAzureFunctions;
    private readonly bool _ignoreControllers;
    private readonly bool _enhancedDiDetection;
    private static bool _msbuildRegistered = false;

    public RoslynAnalyzer(bool verbose = false, bool ignoreTests = false, bool ignoreMigrations = false, 
        bool ignoreAzureFunctions = false, bool ignoreControllers = false, bool enhancedDiDetection = false)
    {
        _verbose = verbose;
        _ignoreTests = ignoreTests;
        _ignoreMigrations = ignoreMigrations;
        _ignoreAzureFunctions = ignoreAzureFunctions;
        _ignoreControllers = ignoreControllers;
        _enhancedDiDetection = enhancedDiDetection;
        EnsureMSBuildRegistered();
    }

    private static void EnsureMSBuildRegistered()
    {
        if (!_msbuildRegistered)
        {
            try
            {
                MSBuildLocator.RegisterDefaults();
                _msbuildRegistered = true;
            }
            catch (InvalidOperationException)
            {
                // MSBuild already registered
                _msbuildRegistered = true;
            }
        }
    }

    /// <summary>
    /// Analyzes a solution file using Roslyn
    /// </summary>
    public async Task<List<ProjectAnalysisResult>> AnalyzeSolutionAsync(string solutionPath)
    {
        var results = new List<ProjectAnalysisResult>();

        try
        {
            using var workspace = MSBuildWorkspace.Create();
            var solution = await workspace.OpenSolutionAsync(solutionPath);

            foreach (var project in solution.Projects)
            {
                if (project.Language == LanguageNames.CSharp)
                {
                    // Filter out test projects if requested
                    if (_ignoreTests && IsTestProject(project))
                    {
                        if (_verbose)
                        {
                            Console.WriteLine($"  Detected test project by Roslyn: {project.Name}");
                        }
                        continue;
                    }
                    
                    var result = await AnalyzeProjectAsync(project);
                    results.Add(result);
                }
            }
        }
        catch (Exception ex)
        {
            if (_verbose)
            {
                Console.WriteLine($"Roslyn analysis failed: {ex.Message}");
            }
            // Return empty results to trigger fallback
            return new List<ProjectAnalysisResult>();
        }

        return results;
    }

    /// <summary>
    /// Analyzes a project file using Roslyn
    /// </summary>
    public async Task<ProjectAnalysisResult> AnalyzeProjectFileAsync(string projectPath)
    {
        try
        {
            using var workspace = MSBuildWorkspace.Create();
            var project = await workspace.OpenProjectAsync(projectPath);
            return await AnalyzeProjectAsync(project);
        }
        catch (Exception ex)
        {
            if (_verbose)
            {
                Console.WriteLine($"Error analyzing project with Roslyn: {ex.Message}");
            }

            return new ProjectAnalysisResult
            {
                ProjectPath = projectPath,
                SourceFileCount = 0
            };
        }
    }

    private async Task<ProjectAnalysisResult> AnalyzeProjectAsync(Project project)
    {
        var result = new ProjectAnalysisResult
        {
            ProjectPath = project.FilePath ?? project.Name,
            SourceFileCount = project.Documents.Count()
        };

        var compilation = await project.GetCompilationAsync();
        if (compilation == null)
        {
            return result;
        }

        // Collect all symbols in the project
        var allSymbols = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        var usedSymbols = new HashSet<ISymbol>(SymbolEqualityComparer.Default);

        // First pass: collect all declared symbols
        foreach (var document in project.Documents)
        {
            // Skip files based on ignore options
            if (ShouldIgnoreDocument(document))
            {
                if (_verbose)
                {
                    Console.WriteLine($"Skipping document: {document.Name}");
                }
                continue;
            }
            
            var syntaxTree = await document.GetSyntaxTreeAsync();
            if (syntaxTree == null) continue;

            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            var root = await syntaxTree.GetRootAsync();

            // Collect declared symbols
            var declaredSymbols = CollectDeclaredSymbols(root, semanticModel);
            foreach (var symbol in declaredSymbols)
            {
                allSymbols.Add(symbol);
            }
        }

        // Second pass: find symbol usages
        foreach (var document in project.Documents)
        {
            // Skip files based on ignore options
            if (ShouldIgnoreDocument(document))
            {
                continue;
            }
            
            var syntaxTree = await document.GetSyntaxTreeAsync();
            if (syntaxTree == null) continue;

            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            var root = await syntaxTree.GetRootAsync();

            var fileResult = await AnalyzeDocument(document, semanticModel, root, allSymbols, usedSymbols);
            result.FileResults.Add(fileResult);

            result.TotalMethods += fileResult.MethodCount;
            result.TotalClasses += fileResult.ClassCount;
        }

        // Identify dead code
        var deadSymbols = allSymbols.Except(usedSymbols, SymbolEqualityComparer.Default).ToList();
        
        // Update results with dead code information
        foreach (var fileResult in result.FileResults)
        {
            UpdateFileResultWithDeadCode(fileResult, deadSymbols);
            result.PotentialDeadMethods += fileResult.PotentialDeadMethodCount;
            result.PotentialDeadClasses += fileResult.PotentialDeadClassCount;
        }

        return result;
    }

    private static List<ISymbol> CollectDeclaredSymbols(SyntaxNode root, SemanticModel semanticModel)
    {
        var symbols = new List<ISymbol>();

        // Collect class declarations
        var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        foreach (var classDecl in classDeclarations)
        {
            var symbol = semanticModel.GetDeclaredSymbol(classDecl);
            if (symbol != null && IsUserDefinedSymbol(symbol))
            {
                symbols.Add(symbol);
            }
        }

        // Collect method declarations (including extension methods)
        var methodDeclarations = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
        foreach (var methodDecl in methodDeclarations)
        {
            var symbol = semanticModel.GetDeclaredSymbol(methodDecl);
            if (symbol != null && IsUserDefinedSymbol(symbol))
            {
                symbols.Add(symbol);
            }
        }

        // Collect property declarations
        var propertyDeclarations = root.DescendantNodes().OfType<PropertyDeclarationSyntax>();
        foreach (var propDecl in propertyDeclarations)
        {
            var symbol = semanticModel.GetDeclaredSymbol(propDecl);
            if (symbol != null && IsUserDefinedSymbol(symbol))
            {
                symbols.Add(symbol);
            }
        }

        return symbols;
    }

    private async Task<FileAnalysisResult> AnalyzeDocument(
        Document document, 
        SemanticModel semanticModel, 
        SyntaxNode root,
        HashSet<ISymbol> allSymbols,
        HashSet<ISymbol> usedSymbols)
    {
        var result = new FileAnalysisResult
        {
            FilePath = document.FilePath ?? document.Name,
            RelativePath = Path.GetRelativePath(Directory.GetCurrentDirectory(), document.FilePath ?? document.Name)
        };

        try
        {
            // Count classes and methods
            result.ClassCount = root.DescendantNodes().OfType<ClassDeclarationSyntax>().Count();
            result.MethodCount = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Count();

            // Find symbol references
            var identifiers = root.DescendantNodes().OfType<IdentifierNameSyntax>();
            foreach (var identifier in identifiers)
            {
                var symbolInfo = semanticModel.GetSymbolInfo(identifier);
                if (symbolInfo.Symbol != null && allSymbols.Contains(symbolInfo.Symbol))
                {
                    usedSymbols.Add(symbolInfo.Symbol);
                }
            }

            // Find member access expressions (including extension method calls)
            var memberAccesses = root.DescendantNodes().OfType<MemberAccessExpressionSyntax>();
            foreach (var memberAccess in memberAccesses)
            {
                var symbolInfo = semanticModel.GetSymbolInfo(memberAccess);
                if (symbolInfo.Symbol != null && allSymbols.Contains(symbolInfo.Symbol))
                {
                    usedSymbols.Add(symbolInfo.Symbol);
                }
            }

            // Find invocation expressions (method calls, including extension methods)
            var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
            foreach (var invocation in invocations)
            {
                var symbolInfo = semanticModel.GetSymbolInfo(invocation);
                if (symbolInfo.Symbol != null && allSymbols.Contains(symbolInfo.Symbol))
                {
                    usedSymbols.Add(symbolInfo.Symbol);
                    
                    // For extension methods, also mark the method as used
                    if (symbolInfo.Symbol is IMethodSymbol method && method.IsExtensionMethod)
                    {
                        usedSymbols.Add(method);
                        if (_verbose)
                        {
                            Console.WriteLine($"    Found extension method usage: {method.Name} in {result.RelativePath}");
                        }
                    }
                }
            }

            // Find object creation expressions
            var objectCreations = root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>();
            foreach (var objectCreation in objectCreations)
            {
                var symbolInfo = semanticModel.GetSymbolInfo(objectCreation);
                if (symbolInfo.Symbol != null && allSymbols.Contains(symbolInfo.Symbol))
                {
                    usedSymbols.Add(symbolInfo.Symbol);
                }
            }

            // Find type references
            var typeReferences = root.DescendantNodes().OfType<TypeSyntax>();
            foreach (var typeRef in typeReferences)
            {
                var symbolInfo = semanticModel.GetSymbolInfo(typeRef);
                if (symbolInfo.Symbol != null && allSymbols.Contains(symbolInfo.Symbol))
                {
                    usedSymbols.Add(symbolInfo.Symbol);
                }
            }

            // Enhanced Dependency Injection detection
            if (_enhancedDiDetection)
            {
                await AnalyzeDependencyInjectionPatterns(root, semanticModel, allSymbols, usedSymbols, result);
            }

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
                Console.WriteLine($"Error analyzing {document.FilePath}: {ex.Message}");
            }
        }

        return result;
    }

    /// <summary>
    /// Analyzes dependency injection patterns to mark classes as used even when only registered in DI containers
    /// </summary>
    private async Task AnalyzeDependencyInjectionPatterns(
        SyntaxNode root, 
        SemanticModel semanticModel, 
        HashSet<ISymbol> allSymbols, 
        HashSet<ISymbol> usedSymbols,
        FileAnalysisResult result)
    {
        // Find all invocation expressions that might be DI registrations
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
        
        foreach (var invocation in invocations)
        {
            var memberAccess = invocation.Expression as MemberAccessExpressionSyntax;
            if (memberAccess == null) continue;

            var methodName = memberAccess.Name.Identifier.ValueText;
            
            // Check for common DI registration method names
            var diRegistrationMethods = new[]
            {
                "AddScoped", "AddTransient", "AddSingleton", "AddHostedService",
                "TryAddScoped", "TryAddTransient", "TryAddSingleton",
                "Register", "RegisterType", "RegisterInstance", "RegisterSingleton",
                "Bind", "BindToConstant", "BindToMethod", "BindToSelf",
                "For", "Use", "Configure", "ConfigureOptions"
            };

            if (!diRegistrationMethods.Contains(methodName)) continue;

            // Analyze generic type arguments
            if (memberAccess.Name is GenericNameSyntax genericName)
            {
                foreach (var typeArg in genericName.TypeArgumentList.Arguments)
                {
                    var typeSymbol = semanticModel.GetSymbolInfo(typeArg).Symbol;
                    if (typeSymbol != null && allSymbols.Contains(typeSymbol))
                    {
                        usedSymbols.Add(typeSymbol);
                        if (_verbose)
                        {
                            Console.WriteLine($"    DI Registration: {typeSymbol.Name} marked as USED via {methodName}<{typeArg}> in {result.RelativePath}");
                        }
                    }
                }
            }

            // Analyze typeof() expressions in method arguments
            foreach (var argument in invocation.ArgumentList.Arguments)
            {
                var typeofExpressions = argument.DescendantNodes().OfType<TypeOfExpressionSyntax>();
                foreach (var typeofExpr in typeofExpressions)
                {
                    var typeSymbol = semanticModel.GetSymbolInfo(typeofExpr.Type).Symbol;
                    if (typeSymbol != null && allSymbols.Contains(typeSymbol))
                    {
                        usedSymbols.Add(typeSymbol);
                        if (_verbose)
                        {
                            Console.WriteLine($"    DI Registration: {typeSymbol.Name} marked as USED via typeof() in {methodName} in {result.RelativePath}");
                        }
                    }
                }
            }
        }

        // Find constructor parameters that might indicate DI usage
        var constructors = root.DescendantNodes().OfType<ConstructorDeclarationSyntax>();
        foreach (var constructor in constructors)
        {
            foreach (var parameter in constructor.ParameterList.Parameters)
            {
                if (parameter.Type != null)
                {
                    var parameterTypeSymbol = semanticModel.GetSymbolInfo(parameter.Type).Symbol;
                    if (parameterTypeSymbol != null && allSymbols.Contains(parameterTypeSymbol))
                    {
                        usedSymbols.Add(parameterTypeSymbol);
                        if (_verbose)
                        {
                            Console.WriteLine($"    Constructor Injection: {parameterTypeSymbol.Name} marked as USED as constructor parameter in {result.RelativePath}");
                        }
                    }
                }
            }
        }

        // Find method parameters that might indicate DI usage
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
        foreach (var method in methods)
        {
            // Check for [FromServices] attribute or similar DI attributes
            foreach (var parameter in method.ParameterList.Parameters)
            {
                var hasServiceAttribute = parameter.AttributeLists
                    .SelectMany(al => al.Attributes)
                    .Any(attr => 
                    {
                        var attrName = attr.Name.ToString();
                        return attrName.Contains("FromServices") || 
                               attrName.Contains("Inject") || 
                               attrName.Contains("Dependency");
                    });

                if (hasServiceAttribute && parameter.Type != null)
                {
                    var parameterTypeSymbol = semanticModel.GetSymbolInfo(parameter.Type).Symbol;
                    if (parameterTypeSymbol != null && allSymbols.Contains(parameterTypeSymbol))
                    {
                        usedSymbols.Add(parameterTypeSymbol);
                        if (_verbose)
                        {
                            Console.WriteLine($"    Method Injection: {parameterTypeSymbol.Name} marked as USED via attribute injection in {result.RelativePath}");
                        }
                    }
                }
            }
        }

        // Find property injection patterns
        var properties = root.DescendantNodes().OfType<PropertyDeclarationSyntax>();
        foreach (var property in properties)
        {
            var hasServiceAttribute = property.AttributeLists
                .SelectMany(al => al.Attributes)
                .Any(attr => 
                {
                    var attrName = attr.Name.ToString();
                    return attrName.Contains("Inject") || 
                           attrName.Contains("Dependency") ||
                           attrName.Contains("Autowired");
                });

            if (hasServiceAttribute)
            {
                var propertyTypeSymbol = semanticModel.GetSymbolInfo(property.Type).Symbol;
                if (propertyTypeSymbol != null && allSymbols.Contains(propertyTypeSymbol))
                {
                    usedSymbols.Add(propertyTypeSymbol);
                    if (_verbose)
                    {
                        Console.WriteLine($"    Property Injection: {propertyTypeSymbol.Name} marked as USED via property injection in {result.RelativePath}");
                    }
                }
            }
        }

        await Task.CompletedTask;
    }

    private void UpdateFileResultWithDeadCode(FileAnalysisResult fileResult, List<ISymbol> deadSymbols)
    {
        foreach (var symbol in deadSymbols)
        {
            var locations = symbol.Locations.Where(loc => loc.IsInSource && 
                                                         loc.SourceTree?.FilePath == fileResult.FilePath);

            foreach (var location in locations)
            {
                var lineSpan = location.GetLineSpan();
                var deadCodeItem = new DeadCodeItem
                {
                    Name = symbol.Name,
                    Type = GetSymbolType(symbol),
                    LineNumber = lineSpan.StartLinePosition.Line + 1,
                    ColumnNumber = lineSpan.StartLinePosition.Character + 1,
                    ConfidencePercentage = CalculateConfidence(symbol),
                    Reason = GetDeadCodeReason(symbol)
                };

                if (symbol is IMethodSymbol)
                {
                    fileResult.DeadMethods.Add(deadCodeItem);
                    fileResult.PotentialDeadMethodCount++;
                }
                else if (symbol is INamedTypeSymbol)
                {
                    fileResult.DeadClasses.Add(deadCodeItem);
                    fileResult.PotentialDeadClassCount++;
                }
            }
        }
    }

    private static bool IsUserDefinedSymbol(ISymbol symbol)
    {
        // Skip compiler-generated symbols
        if (symbol.IsImplicitlyDeclared) return false;
        
        // Skip symbols from external assemblies
        if (symbol.ContainingAssembly?.Name != symbol.ContainingAssembly?.Identity.Name) return false;
        
        // Skip entry points
        if (symbol is IMethodSymbol method && method.Name == "Main") return false;
        
        // Skip constructors for now (they're often called implicitly)
        if (symbol is IMethodSymbol { MethodKind: MethodKind.Constructor }) return false;
        
        // Include extension methods even if they are public (they might be dead code)
        if (symbol is IMethodSymbol extensionMethod && extensionMethod.IsExtensionMethod)
            return true;
        
        // Skip public API members (they might be used externally) - but not extension methods
        if (symbol.DeclaredAccessibility == Accessibility.Public) return false;

        return true;
    }

    private static string GetSymbolType(ISymbol symbol)
    {
        return symbol switch
        {
            IMethodSymbol => "Method",
            INamedTypeSymbol => "Class",
            IPropertySymbol => "Property",
            IFieldSymbol => "Field",
            _ => "Unknown"
        };
    }

    private static int CalculateConfidence(ISymbol symbol)
    {
        // Base confidence
        var confidence = 70;

        // Increase confidence for private members
        if (symbol.DeclaredAccessibility == Accessibility.Private)
            confidence += 20;

        // Decrease confidence for protected members (might be used by derived classes)
        if (symbol.DeclaredAccessibility == Accessibility.Protected)
            confidence -= 30;

        // Decrease confidence for virtual/abstract members
        if (symbol.IsVirtual || symbol.IsAbstract)
            confidence -= 20;

        return Math.Max(10, Math.Min(95, confidence));
    }

    private static string GetDeadCodeReason(ISymbol symbol)
    {
        var accessibility = symbol.DeclaredAccessibility.ToString().ToLower();
        return $"No references found to this {accessibility} {GetSymbolType(symbol).ToLower()}";
    }

    private static bool IsTestProject(Project project)
    {
        try
        {
            var projectName = project.Name;
            
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
                    return true;
                }
            }
            
            // Check project file content for test-related package references
            if (!string.IsNullOrEmpty(project.FilePath) && File.Exists(project.FilePath))
            {
                var projectContent = File.ReadAllText(project.FilePath);
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
                        return true;
                    }
                }
            }
            
            return false;
        }
        catch
        {
            return false;
        }
    }
    
    private bool ShouldIgnoreDocument(Document document)
    {
        var filePath = document.FilePath;
        if (string.IsNullOrEmpty(filePath))
            return false;
            
        // Skip migration files if requested
        if (_ignoreMigrations && IsMigrationFile(filePath))
        {
            return true;
        }
        
        // Skip Azure Function files if requested
        if (_ignoreAzureFunctions && IsAzureFunctionFile(filePath))
        {
            return true;
        }
        
        // Skip Controller files if requested
        if (_ignoreControllers && IsControllerFile(filePath))
        {
            return true;
        }
        
        return false;
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