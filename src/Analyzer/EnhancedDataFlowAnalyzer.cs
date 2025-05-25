using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace DeadSharp.Analyzer;

/// <summary>
/// Enhanced data flow analyzer that tracks variable usage through complex control flows
/// Similar to JetBrains Rider's analysis capabilities
/// </summary>
public class EnhancedDataFlowAnalyzer
{
    private readonly SemanticModel _semanticModel;
    private readonly bool _verbose;
    private readonly HashSet<ISymbol> _usedSymbols;

    public EnhancedDataFlowAnalyzer(SemanticModel semanticModel, bool verbose = false)
    {
        _semanticModel = semanticModel;
        _verbose = verbose;
        _usedSymbols = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
    }

    /// <summary>
    /// Analyzes data flow patterns that basic analysis might miss
    /// </summary>
    public async Task<HashSet<ISymbol>> AnalyzeAdvancedDataFlowAsync(SyntaxNode root, HashSet<ISymbol> allSymbols)
    {
        // 1. Analyze method return value usage
        await AnalyzeMethodReturnUsage(root, allSymbols);
        
        // 2. Analyze factory pattern usage
        AnalyzeFactoryPatterns(root, allSymbols);
        
        // 3. Analyze lambda and delegate usage
        await AnalyzeLambdaAndDelegateUsage(root, allSymbols);
        
        // 4. Analyze conditional usage patterns
        await AnalyzeConditionalUsage(root, allSymbols);
        
        // 5. Analyze interface implementation usage
        AnalyzeInterfaceImplementationUsage(root, allSymbols);
        
        return _usedSymbols;
    }

    /// <summary>
    /// Tracks usage through method return values
    /// Example: var service = GetService(); service.Method();
    /// </summary>
    private async Task AnalyzeMethodReturnUsage(SyntaxNode root, HashSet<ISymbol> allSymbols)
    {
        var variableDeclarations = root.DescendantNodes().OfType<VariableDeclaratorSyntax>();
        
        foreach (var variable in variableDeclarations)
        {
            if (variable.Initializer?.Value is InvocationExpressionSyntax invocation)
            {
                // Get the method being called
                var methodSymbol = _semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
                if (methodSymbol?.ReturnType != null && allSymbols.Contains(methodSymbol.ReturnType))
                {
                    // Mark return type as used
                    _usedSymbols.Add(methodSymbol.ReturnType);
                    
                    // Now track usage of the variable
                    var variableSymbol = _semanticModel.GetDeclaredSymbol(variable);
                    if (variableSymbol != null)
                    {
                        await TrackVariableUsage(root, variableSymbol, allSymbols);
                    }
                    
                    if (_verbose)
                    {
                        Console.WriteLine($"    Data Flow: {methodSymbol.ReturnType.Name} marked as USED via method return");
                    }
                }
            }
        }
    }

    /// <summary>
    /// Analyzes factory pattern usage
    /// Example: var factory = GetFactory(); var service = factory.Create<Service>();
    /// </summary>
    private void AnalyzeFactoryPatterns(SyntaxNode root, HashSet<ISymbol> allSymbols)
    {
        var memberAccesses = root.DescendantNodes().OfType<MemberAccessExpressionSyntax>();
        
        foreach (var memberAccess in memberAccesses)
        {
            // Look for factory method patterns
            var methodName = memberAccess.Name.Identifier.ValueText;
            if (IsFactoryMethod(methodName))
            {
                // Check if this is a generic factory call
                if (memberAccess.Name is GenericNameSyntax genericName)
                {
                    foreach (var typeArg in genericName.TypeArgumentList.Arguments)
                    {
                        var typeSymbol = _semanticModel.GetSymbolInfo(typeArg).Symbol;
                        if (typeSymbol != null && allSymbols.Contains(typeSymbol))
                        {
                            _usedSymbols.Add(typeSymbol);
                            
                            if (_verbose)
                            {
                                Console.WriteLine($"    Factory Pattern: {typeSymbol.Name} marked as USED via factory method");
                            }
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Analyzes lambda expressions and delegate usage
    /// </summary>
    private async Task AnalyzeLambdaAndDelegateUsage(SyntaxNode root, HashSet<ISymbol> allSymbols)
    {
        // Analyze lambda expressions
        var lambdas = root.DescendantNodes().OfType<LambdaExpressionSyntax>();
        foreach (var lambda in lambdas)
        {
            await AnalyzeLambdaBody(lambda, allSymbols);
        }

        // Analyze anonymous methods
        var anonymousMethods = root.DescendantNodes().OfType<AnonymousMethodExpressionSyntax>();
        foreach (var method in anonymousMethods)
        {
            await AnalyzeAnonymousMethodBody(method, allSymbols);
        }
    }

    /// <summary>
    /// Analyzes conditional usage patterns that might be missed
    /// </summary>
    private async Task AnalyzeConditionalUsage(SyntaxNode root, HashSet<ISymbol> allSymbols)
    {
        var ifStatements = root.DescendantNodes().OfType<IfStatementSyntax>();
        
        foreach (var ifStatement in ifStatements)
        {
            // Use Roslyn's control flow analysis
            var controlFlow = _semanticModel.AnalyzeControlFlow(ifStatement);
            if (controlFlow.Succeeded)
            {
                // Analyze what happens in each branch
                await AnalyzeControlFlowBranch(ifStatement.Statement, allSymbols);
                
                if (ifStatement.Else != null)
                {
                    await AnalyzeControlFlowBranch(ifStatement.Else.Statement, allSymbols);
                }
            }
        }
    }

    /// <summary>
    /// Analyzes interface implementation usage
    /// Marks implementing classes as used when interface is used
    /// </summary>
    private void AnalyzeInterfaceImplementationUsage(SyntaxNode root, HashSet<ISymbol> allSymbols)
    {
        var typeReferences = root.DescendantNodes().OfType<TypeSyntax>();
        
        foreach (var typeRef in typeReferences)
        {
            var typeSymbol = _semanticModel.GetSymbolInfo(typeRef).Symbol as ITypeSymbol;
            if (typeSymbol?.TypeKind == TypeKind.Interface)
            {
                // Find all implementations of this interface
                var implementations = FindInterfaceImplementations(typeSymbol, allSymbols);
                foreach (var implementation in implementations)
                {
                    _usedSymbols.Add(implementation);
                    
                    if (_verbose)
                    {
                        Console.WriteLine($"    Interface Usage: {implementation.Name} marked as USED via interface {typeSymbol.Name}");
                    }
                }
            }
        }
    }

    /// <summary>
    /// Tracks how a variable is used throughout the method
    /// </summary>
    private async Task TrackVariableUsage(SyntaxNode scope, ISymbol variableSymbol, HashSet<ISymbol> allSymbols)
    {
        var identifiers = scope.DescendantNodes().OfType<IdentifierNameSyntax>()
            .Where(id => id.Identifier.ValueText == variableSymbol.Name);

        foreach (var identifier in identifiers)
        {
            var symbolInfo = _semanticModel.GetSymbolInfo(identifier);
            if (SymbolEqualityComparer.Default.Equals(symbolInfo.Symbol, variableSymbol))
            {
                // This is a usage of our variable - analyze what's being done with it
                await AnalyzeVariableUsageContext(identifier, allSymbols);
            }
        }
    }

    /// <summary>
    /// Analyzes the context in which a variable is used
    /// </summary>
    private Task AnalyzeVariableUsageContext(IdentifierNameSyntax identifier, HashSet<ISymbol> allSymbols)
    {
        var parent = identifier.Parent;
        
        switch (parent)
        {
            case MemberAccessExpressionSyntax memberAccess when memberAccess.Expression == identifier:
                // Variable is being used to access a member
                var memberSymbol = _semanticModel.GetSymbolInfo(memberAccess).Symbol;
                if (memberSymbol != null && allSymbols.Contains(memberSymbol))
                {
                    _usedSymbols.Add(memberSymbol);
                }
                break;
                
            case InvocationExpressionSyntax invocation:
                // Variable is being invoked (delegate/function pointer)
                var invokedSymbol = _semanticModel.GetSymbolInfo(invocation).Symbol;
                if (invokedSymbol != null && allSymbols.Contains(invokedSymbol))
                {
                    _usedSymbols.Add(invokedSymbol);
                }
                break;
                
            case ArgumentSyntax argument:
                // Variable is being passed as an argument
                AnalyzeArgumentUsage(argument, identifier, allSymbols);
                break;
        }
        
        return Task.CompletedTask;
    }

    /// <summary>
    /// Analyzes lambda body for symbol usage
    /// </summary>
    private Task AnalyzeLambdaBody(LambdaExpressionSyntax lambda, HashSet<ISymbol> allSymbols)
    {
        SyntaxNode? body = lambda switch
        {
            SimpleLambdaExpressionSyntax simple => simple.Body,
            ParenthesizedLambdaExpressionSyntax parenthesized => parenthesized.Body,
            _ => null
        };

        if (body != null)
        {
            AnalyzeNodeForSymbolUsage(body, allSymbols);
        }
        
        return Task.CompletedTask;
    }

    /// <summary>
    /// Analyzes anonymous method body for symbol usage
    /// </summary>
    private Task AnalyzeAnonymousMethodBody(AnonymousMethodExpressionSyntax method, HashSet<ISymbol> allSymbols)
    {
        if (method.Body != null)
        {
            AnalyzeNodeForSymbolUsage(method.Body, allSymbols);
        }
        
        return Task.CompletedTask;
    }

    /// <summary>
    /// Analyzes control flow branch for symbol usage
    /// </summary>
    private Task AnalyzeControlFlowBranch(StatementSyntax statement, HashSet<ISymbol> allSymbols)
    {
        AnalyzeNodeForSymbolUsage(statement, allSymbols);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Analyzes argument usage to understand parameter types
    /// </summary>
    private void AnalyzeArgumentUsage(ArgumentSyntax argument, IdentifierNameSyntax identifier, HashSet<ISymbol> allSymbols)
    {
        // Find the method being called
        var invocation = argument.FirstAncestorOrSelf<InvocationExpressionSyntax>();
        if (invocation != null)
        {
            var methodSymbol = _semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
            if (methodSymbol != null)
            {
                // Find which parameter this argument corresponds to
                var argumentList = argument.Parent as ArgumentListSyntax;
                if (argumentList != null)
                {
                    var argumentIndex = argumentList.Arguments.IndexOf(argument);
                    if (argumentIndex >= 0 && argumentIndex < methodSymbol.Parameters.Length)
                    {
                        var parameterType = methodSymbol.Parameters[argumentIndex].Type;
                        if (allSymbols.Contains(parameterType))
                        {
                            _usedSymbols.Add(parameterType);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Generic method to analyze any syntax node for symbol usage
    /// </summary>
    private void AnalyzeNodeForSymbolUsage(SyntaxNode node, HashSet<ISymbol> allSymbols)
    {
        var identifiers = node.DescendantNodes().OfType<IdentifierNameSyntax>();
        foreach (var identifier in identifiers)
        {
            var symbolInfo = _semanticModel.GetSymbolInfo(identifier);
            if (symbolInfo.Symbol != null && allSymbols.Contains(symbolInfo.Symbol))
            {
                _usedSymbols.Add(symbolInfo.Symbol);
            }
        }
    }

    /// <summary>
    /// Finds all classes that implement a given interface
    /// </summary>
    private IEnumerable<ISymbol> FindInterfaceImplementations(ITypeSymbol interfaceSymbol, HashSet<ISymbol> allSymbols)
    {
        return allSymbols
            .OfType<INamedTypeSymbol>()
            .Where(type => type.TypeKind == TypeKind.Class && 
                          type.AllInterfaces.Contains(interfaceSymbol, SymbolEqualityComparer.Default));
    }

    /// <summary>
    /// Determines if a method name indicates a factory pattern
    /// </summary>
    private static bool IsFactoryMethod(string methodName)
    {
        var factoryMethods = new[]
        {
            "Create", "CreateInstance", "Build", "Make", "New", "Get", "Resolve",
            "Factory", "Builder", "Generator", "Provider", "Activator"
        };

        return factoryMethods.Any(pattern => 
            methodName.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }
} 