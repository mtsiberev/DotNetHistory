using System.Diagnostics;

namespace DotNetHistory.Services;

public class SandboxResult
{
    public string Output { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public bool IsSuccess => string.IsNullOrEmpty(ErrorMessage);
}

public class CodeSandboxService
{
    private readonly string _workingDir;
    private readonly string _csprojPath;
    private readonly string _programPath;
    private readonly string _featureCodePath;
    private readonly string _launcherPath;
    private readonly string _dllPath;
    private readonly object _lock = new();

    public CodeSandboxService()
    {
        _workingDir = Path.Combine(Path.GetTempPath(), "DotNetOnlineSandbox_Shared");
        _csprojPath = Path.Combine(_workingDir, "SandboxApp.csproj");
        _programPath = Path.Combine(_workingDir, "Program.cs");
        _featureCodePath = Path.Combine(_workingDir, "FeatureCode.cs");
        _launcherPath = Path.Combine(_workingDir, "Launcher.cs");

        int currentTargetVersion = Environment.Version.Major;
        _dllPath = Path.Combine(_workingDir, "bin", "Release", $"net{currentTargetVersion}.0", "SandboxApp.dll");

        InitializeSandboxProject(currentTargetVersion);
    }

    private void InitializeSandboxProject(int targetVersion)
    {
        if (!Directory.Exists(_workingDir))
        {
            Directory.CreateDirectory(_workingDir);
        }

        string csprojContent = $"""
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <OutputType>Exe</OutputType>
            <TargetFramework>net{targetVersion}.0</TargetFramework>
            <ImplicitUsings>enable</ImplicitUsings>
            <Nullable>enable</Nullable>
            <CopyLocalLockFileAssemblies>false</CopyLocalLockFileAssemblies>
            <RunAnalyzersDuringBuild>false</RunAnalyzersDuringBuild>
          </PropertyGroup>
        </Project>
        """;

        File.WriteAllText(_csprojPath, csprojContent);
        WipeSandboxCsFiles();

        using var restoreProcess = new Process();
        restoreProcess.StartInfo.FileName = "dotnet";
        restoreProcess.StartInfo.Arguments = "restore";
        restoreProcess.StartInfo.WorkingDirectory = _workingDir;
        restoreProcess.StartInfo.UseShellExecute = false;
        restoreProcess.StartInfo.CreateNoWindow = true;
        restoreProcess.StartInfo.EnvironmentVariables["DOTNET_NOLOGO"] = "true";

        restoreProcess.Start();
        restoreProcess.WaitForExit();
    }

    private void WipeSandboxCsFiles()
    {
        if (!Directory.Exists(_workingDir)) return;

        var csFiles = Directory.GetFiles(_workingDir, "*.cs");
        foreach (var file in csFiles)
        {
            try
            {
                File.Delete(file);
            }
            catch { }
        }
    }

    public async Task<SandboxResult> ExecuteFeatureAsync(string classCode, int timeoutMilliseconds = 5000)
    {
        lock (_lock)
        {
            WipeSandboxCsFiles();

            File.WriteAllText(_featureCodePath, classCode);

            string launcherContent = """
            using System;
            using System.Linq;
            using System.Reflection;

            public class SandboxMainLauncher
            {
                public static void Main(string[] args)
                {
                    try
                    {
                        var runnerType = Assembly.GetExecutingAssembly().GetTypes()
                            .FirstOrDefault(t => t.IsClass && !t.IsAbstract && t.Name != "SandboxMainLauncher");

                        if (runnerType != null)
                        {
                            dynamic runner = Activator.CreateInstance(runnerType);
                            var result = runner.Execute();
                            if (result is string str && !string.IsNullOrEmpty(str))
                            {
                                Console.WriteLine(str);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine("Ошибка выполнения: " + (ex.InnerException?.Message ?? ex.Message));
                    }
                }
            }
            """;
            File.WriteAllText(_launcherPath, launcherContent);
        }

        return await BuildAndRunAsync(timeoutMilliseconds);
    }

    public async Task<SandboxResult> ExecuteScriptAsync(string scriptCode, int timeoutMilliseconds = 5000)
    {
        lock (_lock)
        {
            WipeSandboxCsFiles();

            string fullScript = $"""
            using System;
            using System.Collections.Generic;
            using System.Linq;
            using System.Text;
            using System.Threading.Tasks;
            using System.IO;
            using System.Buffers;
            using System.Diagnostics;

            {scriptCode}
            """;

            File.WriteAllText(_programPath, fullScript);
        }

        return await BuildAndRunAsync(timeoutMilliseconds);
    }

    private async Task<SandboxResult> BuildAndRunAsync(int timeoutMilliseconds)
    {
        var result = new SandboxResult();

        using (var buildProcess = new Process())
        {
            buildProcess.StartInfo.FileName = "dotnet";
            buildProcess.StartInfo.Arguments = "build --no-restore -c Release -v m";
            buildProcess.StartInfo.WorkingDirectory = _workingDir;
            buildProcess.StartInfo.UseShellExecute = false;
            buildProcess.StartInfo.RedirectStandardOutput = true;
            buildProcess.StartInfo.RedirectStandardError = true;
            buildProcess.StartInfo.CreateNoWindow = true;
            buildProcess.StartInfo.EnvironmentVariables["DOTNET_NOLOGO"] = "true";

            buildProcess.Start();
            string buildOutput = buildProcess.StandardOutput.ReadToEnd();
            buildProcess.WaitForExit();

            if (buildProcess.ExitCode != 0)
            {
                result.ErrorMessage = $"Compilation error:\n{buildOutput}";
                return result;
            }
        }

        using (var runProcess = new Process())
        {
            runProcess.StartInfo.FileName = "dotnet";
            runProcess.StartInfo.Arguments = $"\"{_dllPath}\"";
            runProcess.StartInfo.WorkingDirectory = _workingDir;
            runProcess.StartInfo.UseShellExecute = false;
            runProcess.StartInfo.RedirectStandardOutput = true;
            runProcess.StartInfo.RedirectStandardError = true;
            runProcess.StartInfo.CreateNoWindow = true;
            runProcess.StartInfo.EnvironmentVariables["DOTNET_NOLOGO"] = "true";

            try
            {
                runProcess.Start();
                using var cts = new CancellationTokenSource(timeoutMilliseconds);
                try
                {
                    await runProcess.WaitForExitAsync(cts.Token);
                    string output = await runProcess.StandardOutput.ReadToEndAsync();
                    string errors = await runProcess.StandardError.ReadToEndAsync();

                    if (runProcess.ExitCode != 0 || !string.IsNullOrEmpty(errors))
                    {
                        result.ErrorMessage = string.IsNullOrWhiteSpace(errors) ? output : errors;
                    }
                    else
                    {
                        result.Output = output;
                    }
                }
                catch (OperationCanceledException)
                {
                    runProcess.Kill(entireProcessTree: true);
                    result.ErrorMessage = $"Execution timeout expired ({timeoutMilliseconds} ms).";
                }
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"Sandbox system crash: {ex.Message}";
            }
        }

        return result;
    }
}
