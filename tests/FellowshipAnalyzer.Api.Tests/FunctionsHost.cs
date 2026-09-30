using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

using Xunit;

namespace FellowshipAnalyzer.Api.Tests;

/// <summary>
/// Runs the Azure Functions host over a published FellowshipAnalyzer.Api, the same way Static Web
/// Apps runs the deployed API.
/// </summary>
/// <remarks>
/// Set <c>FELLOWSHIP_API_PUBLISH_DIR</c> to test an existing publish output. Otherwise the fixture
/// publishes the API in Release to a temporary directory. The host is Azure Functions Core Tools:
/// <c>func</c> on PATH, or <c>FUNC_PATH</c> set to the executable. The worker targets net8.0, so the
/// .NET 8 runtime must be installed.
/// </remarks>
public sealed class FunctionsHost : IAsyncLifetime
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(3);

    private readonly StringBuilder _output = new();
    private Process? _process;
    private string? _temporaryPublishDirectory;

    public string PublishDirectory { get; private set; } = "";

    public HttpClient Client { get; private set; } = new();

    /// <summary>Everything the host and the dotnet publish wrote so far, for failure messages.</summary>
    public string Output
    {
        get
        {
            lock (_output)
            {
                return _output.ToString();
            }
        }
    }

    public async Task InitializeAsync()
    {
        PublishDirectory = Environment.GetEnvironmentVariable("FELLOWSHIP_API_PUBLISH_DIR") is { Length: > 0 } configured
            ? Path.GetFullPath(configured)
            : await PublishAsync();

        var port = FreePort();
        Client = new HttpClient
        {
            BaseAddress = new Uri($"http://localhost:{port}/"),
            Timeout = TimeSpan.FromMinutes(1),
        };

        _process = Start(HostStartInfo(port));
        await WaitUntilRunningAsync(_process);
    }

    public Task DisposeAsync()
    {
        Client.Dispose();

        if (_process is { HasExited: false })
        {
            _process.Kill(entireProcessTree: true);
            _process.WaitForExit(TimeSpan.FromSeconds(30));
        }
        _process?.Dispose();

        if (_temporaryPublishDirectory is not null)
        {
            try
            {
                Directory.Delete(_temporaryPublishDirectory, recursive: true);
            }
            catch (IOException)
            {
            }
        }

        return Task.CompletedTask;
    }

    private async Task<string> PublishAsync()
    {
        _temporaryPublishDirectory = Path.Combine(Path.GetTempPath(), $"fellowship-api-{Guid.NewGuid():N}");
        var project = Path.Combine(RepositoryRoot(), "src", "FellowshipAnalyzer", "FellowshipAnalyzer.Api", "FellowshipAnalyzer.Api.csproj");

        var publish = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        {
            ArgumentList = { "publish", project, "-c", "Release", "-o", _temporaryPublishDirectory, "-nologo" },
        };

        using var process = Start(publish);
        await process.WaitForExitAsync();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"dotnet publish of the API failed with exit code {process.ExitCode}.{Environment.NewLine}{Output}");
        }

        return _temporaryPublishDirectory;
    }

    private ProcessStartInfo HostStartInfo(int port)
    {
        var func = FuncExecutable();
        var start = func.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase)
            ? new ProcessStartInfo("cmd.exe") { ArgumentList = { "/c", func } }
            : new ProcessStartInfo(func);

        start.ArgumentList.Add("start");
        start.ArgumentList.Add("--port");
        start.ArgumentList.Add(port.ToString(CultureInfo.InvariantCulture));
        start.WorkingDirectory = PublishDirectory;
        start.Environment["FUNCTIONS_WORKER_RUNTIME"] = "dotnet-isolated";
        start.Environment["AzureWebJobsStorage"] = "";
        start.Environment["ConnectionStrings__BlobsConnection"] = "UseDevelopmentStorage=true";
        start.Environment["FellowshipLogs__ClientId"] = "functions-host-tests";
        start.Environment["FellowshipLogs__ClientSecret"] = "functions-host-tests";
        return start;
    }

    private Process Start(ProcessStartInfo start)
    {
        start.UseShellExecute = false;
        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;

        var process = new Process { StartInfo = start };
        process.OutputDataReceived += (_, e) => Append(e.Data);
        process.ErrorDataReceived += (_, e) => Append(e.Data);
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return process;
    }

    private void Append(string? line)
    {
        if (line is null)
        {
            return;
        }

        lock (_output)
        {
            _output.AppendLine(line);
        }
    }

    private async Task WaitUntilRunningAsync(Process process)
    {
        var deadline = DateTime.UtcNow + StartupTimeout;
        while (DateTime.UtcNow < deadline)
        {
            if (process.HasExited)
            {
                throw new InvalidOperationException(
                    $"The Functions host exited with code {process.ExitCode}.{Environment.NewLine}{Output}");
            }

            try
            {
                using var response = await Client.GetAsync("admin/host/status");
                if (response.StatusCode == HttpStatusCode.OK
                    && (await response.Content.ReadAsStringAsync()).Contains("\"Running\"", StringComparison.Ordinal))
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500));
        }

        throw new TimeoutException(
            $"The Functions host did not reach the Running state within {StartupTimeout}.{Environment.NewLine}{Output}");
    }

    private static string FuncExecutable()
    {
        if (Environment.GetEnvironmentVariable("FUNC_PATH") is { Length: > 0 } configured)
        {
            return configured;
        }

        string[] names = OperatingSystem.IsWindows() ? ["func.exe", "func.cmd"] : ["func"];
        var directories = (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

        return directories
            .SelectMany(directory => names.Select(name => Path.Combine(directory, name)))
            .FirstOrDefault(File.Exists)
            ?? throw new InvalidOperationException(
                "Azure Functions Core Tools was not found. Put func on PATH or set FUNC_PATH to the func executable.");
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "FellowshipAnalyzer.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException($"No FellowshipAnalyzer.slnx above {AppContext.BaseDirectory}.");
    }
}
