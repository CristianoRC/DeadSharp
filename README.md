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

# Ignorar projetos de teste durante a análise
deadsharp -p /caminho/para/seu/projeto --ignore-tests

# Combinar opções
deadsharp -p /caminho/para/seu/projeto -v --ignore-tests
```

### Tipos de Entrada Suportados

- **Diretórios**: Analisa todos os arquivos .csproj e .sln encontrados
- **Arquivos .sln**: Analisa todos os projetos na solução
- **Arquivos .csproj**: Analisa o projeto específico

### Opções Avançadas

#### Ignorar Projetos de Teste (`--ignore-tests`)

Por padrão, a ferramenta analisa todos os projetos encontrados, incluindo projetos de teste. Isso pode gerar muitos falsos positivos, pois métodos de teste são executados pelos frameworks de teste e não são "chamados" diretamente no código.

Use a opção `--ignore-tests` para filtrar automaticamente projetos de teste:

```bash
deadsharp -p /caminho/para/projeto --ignore-tests
```

A ferramenta detecta projetos de teste baseado em:
- **Padrões de nomenclatura**: projetos contendo "test", "tests", "unittest", "spec", etc.
- **Dependências**: projetos que referenciam pacotes como xUnit, NUnit, MSTest, Moq, FluentAssertions, etc.

**Exemplo de resultado:**
- Sem `--ignore-tests`: 89 métodos potencialmente mortos
- Com `--ignore-tests`: 35 métodos potencialmente mortos (54 falsos positivos removidos)

## Funcionalidades

- ✅ Análise de projetos C# para identificar código não utilizado
- ✅ Funciona com arquivos de projeto (.csproj) e solução (.sln)
- ✅ Relatórios detalhados de localização de código morto
- ✅ Validação de entrada com mensagens de erro claras
- ✅ Modo verboso para análise detalhada
- ✅ Opção para ignorar projetos de teste (reduz falsos positivos)
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

