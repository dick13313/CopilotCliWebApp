using CopilotApi.Models;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.NetworkInformation;

namespace CopilotApi.Services;

public class OperationsSupervisor
{
    private const string FrontendProcessName = "frontend-dev";
    private const int FrontendPort = 5173;
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(20);

    private readonly ILogger<OperationsSupervisor> _logger;
    private readonly string _repoRoot;
    private readonly ConcurrentDictionary<string, Process> _processes = new();
    private readonly ConcurrentDictionary<string, ProcessStatus> _statuses = new();
    private readonly ConcurrentQueue<OperationLogEntry> _logs = new();
    private readonly object _logLock = new();
    private const int MaxLogs = 200;

    public OperationsSupervisor(ILogger<OperationsSupervisor> logger)
    {
        _logger = logger;
        _repoRoot = ResolveRepoRoot();
    }

    public OperationsStatus GetStatus()
    {
        var status = new OperationsStatus
        {
            Timestamp = DateTime.UtcNow,
            Frontend = GetFrontendStatus()
        };

        return status;
    }

    public async Task<OperationsActionResult> StartFrontendAsync(CancellationToken cancellationToken)
    {
        if (_processes.TryGetValue(FrontendProcessName, out var existing) && !existing.HasExited)
        {
            return new OperationsActionResult
            {
                Action = "start_frontend",
                Status = "already_running",
                Message = $"Frontend already running (PID {existing.Id}).",
                Snapshot = GetStatus()
            };
        }

        if (await IsPortOpenAsync(FrontendPort, cancellationToken))
        {
            return new OperationsActionResult
            {
                Action = "start_frontend",
                Status = "port_in_use",
                Message = $"Port {FrontendPort} is already in use.",
                Snapshot = GetStatus()
            };
        }

        var frontendDir = Path.Combine(_repoRoot, "Frontend");
        var process = StartProcess(
            FrontendProcessName,
            "npm",
            "run dev",
            frontendDir);

        _processes[FrontendProcessName] = process;

        await WaitForPortAsync(FrontendPort, DefaultTimeout, cancellationToken);

        return new OperationsActionResult
        {
            Action = "start_frontend",
            Status = "started",
            Message = "Frontend dev server started.",
            Snapshot = GetStatus()
        };
    }

    public async Task<OperationsActionResult> StopFrontendAsync(CancellationToken cancellationToken)
    {
        if (!_processes.TryGetValue(FrontendProcessName, out var process))
        {
            return new OperationsActionResult
            {
                Action = "stop_frontend",
                Status = "not_running",
                Message = "Frontend process is not tracked.",
                Snapshot = GetStatus()
            };
        }

        TryStopProcess(process);
        _processes.TryRemove(FrontendProcessName, out _);
        await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);

        return new OperationsActionResult
        {
            Action = "stop_frontend",
            Status = "stopped",
            Message = "Frontend dev server stopped.",
            Snapshot = GetStatus()
        };
    }

    public async Task<DiagnosticsResult> RunDiagnosticsAsync(CancellationToken cancellationToken)
    {
        var diagnostics = new DiagnosticsResult();
        var checks = new List<Func<CancellationToken, Task<DiagnosticCheckResult>>>
        {
            ct => RunCommandAsync("dotnet", "--version", _repoRoot, ct),
            ct => RunCommandAsync("node", "--version", _repoRoot, ct),
            ct => RunCommandAsync("npm", "--version", _repoRoot, ct),
            ct => RunCommandAsync("copilot", "--version", _repoRoot, ct),
            ct => RunCommandAsync("gh", "auth status", _repoRoot, ct)
        };

        foreach (var check in checks)
        {
            diagnostics.Checks.Add(await check(cancellationToken));
        }

        return diagnostics;
    }

    public IReadOnlyList<OperationLogEntry> GetRecentLogs(int count = 50)
    {
        lock (_logLock)
        {
            return _logs.Reverse().Take(count).ToList();
        }
    }

    private ProcessStatus GetFrontendStatus()
    {
        var status = GetOrCreateStatus(FrontendProcessName);
        status.Name = FrontendProcessName;
        status.Port = FrontendPort;
        status.PortOpen = IsPortListening(FrontendPort);

        if (_processes.TryGetValue(FrontendProcessName, out var process))
        {
            status.IsRunning = !process.HasExited;
            status.Pid = process.Id;
            status.StartedAt ??= DateTime.UtcNow;
            if (process.HasExited)
            {
                status.ExitedAt = status.ExitedAt ?? DateTime.UtcNow;
                status.ExitCode = process.ExitCode;
            }
        }
        else
        {
            status.IsRunning = false;
            status.Pid = null;
        }

        status.RecentLogs = GetRecentLogs().ToList();
        return status;
    }

    private Process StartProcess(string name, string fileName, string arguments, string workingDirectory)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
            {
                AppendLog(name, "info", args.Data);
                var status = GetOrCreateStatus(name);
                status.LastOutput = args.Data;
            }
        };
        process.ErrorDataReceived += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
            {
                AppendLog(name, "error", args.Data);
                var status = GetOrCreateStatus(name);
                status.LastError = args.Data;
            }
        };
        process.Exited += (_, _) =>
        {
            AppendLog(name, "info", $"Process exited with code {process.ExitCode}.");
            var status = GetOrCreateStatus(name);
            status.ExitedAt = DateTime.UtcNow;
            status.ExitCode = process.ExitCode;
        };

        if (!process.Start())
        {
            throw new InvalidOperationException($"Failed to start {name}.");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        AppendLog(name, "info", $"Process started (PID {process.Id}).");

        var status = GetOrCreateStatus(name);
        status.StartedAt = DateTime.UtcNow;
        status.Pid = process.Id;
        status.IsRunning = true;

        return process;
    }

    private void TryStopProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop process");
        }
    }

    private async Task<DiagnosticCheckResult> RunCommandAsync(
        string fileName,
        string arguments,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        var result = new DiagnosticCheckResult { Command = $"{fileName} {arguments}" };
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(DefaultTimeout);

            await process.WaitForExitAsync(timeoutCts.Token);

            result.ExitCode = process.ExitCode;
            result.Output = await outputTask;
            result.Error = await errorTask;
        }
        catch (OperationCanceledException)
        {
            result.TimedOut = true;
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
        }

        return result;
    }

    private static async Task WaitForPortAsync(int port, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var start = DateTime.UtcNow;
        while (DateTime.UtcNow - start < timeout)
        {
            if (IsPortListening(port))
            {
                return;
            }
            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
        }
    }

    private static bool IsPortListening(int port)
    {
        try
        {
            var properties = IPGlobalProperties.GetIPGlobalProperties();
            return properties.GetActiveTcpListeners().Any(ep => ep.Port == port);
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> IsPortOpenAsync(int port, CancellationToken cancellationToken)
    {
        return await Task.Run(() => IsPortListening(port), cancellationToken);
    }

    private void AppendLog(string source, string level, string message)
    {
        var entry = new OperationLogEntry
        {
            Timestamp = DateTime.UtcNow,
            Source = source,
            Level = level,
            Message = message
        };

        lock (_logLock)
        {
            _logs.Enqueue(entry);
            while (_logs.Count > MaxLogs)
            {
                _logs.TryDequeue(out _);
            }
        }

        _logger.LogInformation("[{Source}] {Message}", source, message);
    }

    private ProcessStatus GetOrCreateStatus(string name)
    {
        return _statuses.GetOrAdd(name, _ => new ProcessStatus { Name = name });
    }

    private static string ResolveRepoRoot()
    {
        var directory = Directory.GetCurrentDirectory();
        while (!string.IsNullOrWhiteSpace(directory))
        {
            if (Directory.Exists(Path.Combine(directory, "Backend")) &&
                Directory.Exists(Path.Combine(directory, "Frontend")))
            {
                return directory;
            }

            directory = Directory.GetParent(directory)?.FullName ?? string.Empty;
        }

        return Directory.GetCurrentDirectory();
    }
}
