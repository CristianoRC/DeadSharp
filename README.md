# DeadSharp

DeadSharp é uma ferramenta de linha de comando para analisar projetos C# e identificar código morto (dead code).

## Instalação

### Do NuGet (quando publicado)
```bash
dotnet tool install --global DeadSharp
```

### Do Código Fonte
1. Clone o repositório
2. Compile o projeto
   ```bash
   cd src
   dotnet pack
   dotnet tool install --global --add-source ./nupkg DeadSharp
   ```

## Uso

```bash
# Uso básico
deadsharp --path /caminho/para/seu/projeto

# Ou com parâmetro curto
deadsharp -p /caminho/para/seu/projeto

# Habilitar saída verbosa
deadsharp -p /caminho/para/seu/projeto -v
```

### Tipos de Entrada Suportados

- **Diretórios**: Analisa todos os arquivos .csproj e .sln encontrados
- **Arquivos .sln**: Analisa todos os projetos na solução
- **Arquivos .csproj**: Analisa o projeto específico

## Funcionalidades

- ✅ Análise de projetos C# para identificar código não utilizado
- ✅ Funciona com arquivos de projeto (.csproj) e solução (.sln)
- ✅ Relatórios detalhados de localização de código morto
- ✅ Validação de entrada com mensagens de erro claras
- ✅ Modo verboso para análise detalhada
- ✅ Arquitetura modular e extensível

## Estrutura do Projeto

```
src/
├── Program.cs                    # Ponto de entrada principal
├── Commands/
│   ├── CommandLineOptions.cs    # Configuração de opções da linha de comando
│   └── AnalyzeCommand.cs        # Lógica do comando de análise
└── Analyzer/
    ├── CodeAnalyzer.cs          # Analisador principal de código
    └── AnalysisResult.cs        # Modelos de resultado da análise
```

## Desenvolvimento

### Compilar
```bash
cd src
dotnet build
```

### Testar Localmente
```bash
cd src
dotnet run -- --path /caminho/para/projeto --verbose
```

### Empacotar
```bash
cd src
dotnet pack
```

## Licença

Veja o arquivo [LICENSE](LICENSE) para detalhes.


🤝 Contributing
Pull requests are welcome! If you'd like to contribute, please fork the repo and submit a PR. Bug reports and feature requests are also highly appreciated.

