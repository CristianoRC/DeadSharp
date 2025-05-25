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
    private static bool _msbuildRegistered = false;

    public RoslynAnalyzer(bool verbose = false)
    {
        _verbose = verbose;
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
                    var result = await AnalyzeProjectAsync(project);
                    results.Add(result);
                }
            }
        }
        catch (Exception ex)
        {
            if (_verbose)
            {
                Console.WriteLine($"Error analyzing solution with Roslyn: {ex.Message}");
            }
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
            var syntaxTree = await document.GetSyntaxTreeAsync();
            if (syntaxTree == null) continue;

            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            var root = await syntaxTree.GetRootAsync();

            var fileResult = AnalyzeDocument(document, semanticModel, root, allSymbols, usedSymbols);
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

        // Collect method declarations
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

    private FileAnalysisResult AnalyzeDocument(
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

            // Find member access expressions
            var memberAccesses = root.DescendantNodes().OfType<MemberAccessExpressionSyntax>();
            foreach (var memberAccess in memberAccesses)
            {
                var symbolInfo = semanticModel.GetSymbolInfo(memberAccess);
                if (symbolInfo.Symbol != null && allSymbols.Contains(symbolInfo.Symbol))
                {
                    usedSymbols.Add(symbolInfo.Symbol);
                }
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
        
        // Skip public API members (they might be used externally)
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
} 