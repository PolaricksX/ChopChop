using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;

internal static class CleanupHelper
{
    private static readonly string ErrorFile = Path.Combine(Path.GetTempPath(), "ChopChop.LocalLauncher.cleanup-error.log");

    public static void ReportPreviousError()
    {
        if (File.Exists(ErrorFile))
        {
            Console.Error.WriteLine(File.ReadAllText(ErrorFile));
            File.Delete(ErrorFile);
        }
    }

    public static async Task<Process> StartAsync(string repositoryRoot, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = repositoryRoot,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
        startInfo.ArgumentList.Add("--cleanup-helper");
        startInfo.ArgumentList.Add(Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add(repositoryRoot);
        var process = new Process { StartInfo = startInfo };

        var started = false;
        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException("Could not start the test stack cleanup helper.");
            }

            started = true;
            var ready = await process.StandardOutput.ReadLineAsync(cancellationToken);
            if (ready != "READY")
            {
                var error = await process.StandardError.ReadToEndAsync(cancellationToken);
                throw new InvalidOperationException($"The test stack cleanup helper failed to start: {error}");
            }

            return process;
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception or IOException or OperationCanceledException)
        {
            if (started && !process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }

            process.Dispose();
            throw new InvalidOperationException("Could not initialize the test stack cleanup helper.", exception);
        }
    }

    public static async Task<bool> StopAsync(Process process)
    {
        using (process)
        {
            if (!process.HasExited)
            {
                try
                {
                    await process.StandardInput.WriteLineAsync("stop");
                    await process.StandardInput.FlushAsync();
                }
                catch (IOException exception)
                {
                    Console.Error.WriteLine($"Could not signal the test stack cleanup helper: {exception.Message}");
                }
            }

            await process.WaitForExitAsync();
            if (process.ExitCode != 0)
            {
                Console.Error.WriteLine("The test database and RabbitMQ could not be stopped automatically.");
                ReportPreviousError();
                return false;
            }

            Console.WriteLine("Stopped the isolated test database and RabbitMQ; test data was preserved.");
            return true;
        }
    }

    public static async Task<int> RunAsync(string parentId, string repositoryRoot)
    {
        if (!int.TryParse(parentId, CultureInfo.InvariantCulture, out var id))
        {
            Console.Error.WriteLine("Invalid cleanup helper parent process ID.");
            return 1;
        }

        Process parent;
        try
        {
            parent = Process.GetProcessById(id);
        }
        catch (ArgumentException)
        {
            Console.Error.WriteLine("The launcher exited before the cleanup helper started.");
            return 1;
        }

        using (parent)
        {
            Console.WriteLine("READY");
            await Task.WhenAny(parent.WaitForExitAsync(), Console.In.ReadLineAsync());
        }

        try
        {
            var startInfo = new ProcessStartInfo("docker")
            {
                WorkingDirectory = repositoryRoot,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (var argument in new[]
            {
                "compose", "--project-name", "chopchop-test",
                "--env-file", Path.Combine(repositoryRoot, ".env.test"),
                "-f", Path.Combine(repositoryRoot, "compose.test.yaml"), "down"
            })
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var docker = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Could not start Docker Compose.");
            var output = docker.StandardOutput.ReadToEndAsync();
            var error = docker.StandardError.ReadToEndAsync();
            await docker.WaitForExitAsync();
            await Task.WhenAll(output, error);
            if (docker.ExitCode != 0)
            {
                return WriteFailure($"Docker Compose exited with code {docker.ExitCode}.");
            }

            return 0;
        }
        catch (Exception exception) when (exception is Win32Exception or IOException or InvalidOperationException)
        {
            return WriteFailure($"Could not run Docker Compose: {exception.Message}");
        }
    }

    private static int WriteFailure(string reason)
    {
        File.WriteAllText(
            ErrorFile,
            $"Automatic test-stack cleanup failed. {reason}{Environment.NewLine}" +
            "Run: docker compose --project-name chopchop-test --env-file .env.test -f compose.test.yaml down");
        return 1;
    }
}
