using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Text;

namespace DotNetHistory.Services;

public class SandboxResult
{
    public string Output { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public bool IsSuccess => string.IsNullOrEmpty(ErrorMessage);
}

public class CodeSandboxService
{
    private static readonly List<MetadataReference> References;

    static CodeSandboxService()
    {
        References = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "System.Runtime.dll")),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location)
        };
    }

    public async Task<SandboxResult> ExecuteCodeAsync(string userCode, int timeoutMilliseconds = 3000)
    {
        var result = new SandboxResult();

        if (string.IsNullOrWhiteSpace(userCode))
        {
            result.ErrorMessage = "Код не может быть пустым.";
            return result;
        }

        string fullSourceCode = """
        using System;
        using System.Collections.Generic;
        using System.Linq;
        using System.Text;
        using System.Threading.Tasks;

        public class SandboxProgram
        {
            public static void Main()
            {
        """ + $"\n{userCode}\n" + """
            }
        }
        """;

        try
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(fullSourceCode);
            var compilation = CSharpCompilation.Create(
                $"SandboxAssembly_{Guid.NewGuid():N}",
                new[] { syntaxTree },
                References,
                new CSharpCompilationOptions(OutputKind.ConsoleApplication));

            using var ms = new MemoryStream();
            var emitResult = compilation.Emit(ms);

            if (!emitResult.Success)
            {
                var failures = emitResult.Diagnostics.Where(d => d.IsWarningAsError || d.Severity == DiagnosticSeverity.Error);
                var errorBuilder = new StringBuilder("Ошибка компиляции:\n");
                foreach (var diagnostic in failures)
                {
                    errorBuilder.AppendLine($"- {diagnostic.GetMessage()}");
                }
                result.ErrorMessage = errorBuilder.ToString();
                return result;
            }

            ms.Seek(0, SeekOrigin.Begin);

            using var cts = new CancellationTokenSource(timeoutMilliseconds);

            await Task.Run(() =>
            {
                var assembly = AssemblyLoadContext.Default.LoadFromStream(ms);
                var type = assembly.GetType("SandboxProgram");
                var method = type?.GetMethod("Main");

                if (method == null)
                {
                    result.ErrorMessage = "Не удалось найти точку входа в программу.";
                    return;
                }

                var currentOut = Console.Out;
                using var writer = new StringWriter();
                Console.SetOut(writer);

                try
                {
                    method.Invoke(null, null);
                    result.Output = writer.ToString();
                }
                catch (TargetInvocationException ex)
                {
                    result.ErrorMessage = $"Ошибка в рантайме: {ex.InnerException?.Message}";
                }
                finally
                {
                    Console.SetOut(currentOut);
                }
            }, cts.Token);
        }
        catch (OperationCanceledException)
        {
            result.ErrorMessage = $"Превышено время ожидания выполнения ({timeoutMilliseconds} мс). Возможно, в коде бесконечный цикл.";
        }
        catch (Exception ex)
        {
            result.ErrorMessage = $"Системный сбой песочницы: {ex.Message}";
        }

        return result;
    }
}
