using System.Data.Common;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

return await LocalLauncher.RunAsync(args);

internal static class LocalLauncher
{
    private const string ProjectName = "chopchop-test";
    private const string DevelopmentEnvironment = "Development";

    public static async Task<int> RunAsync(string[] args)
    {
        if (args is ["--cleanup-helper", var parentId, var helperRoot])
        {
            return await CleanupHelper.RunAsync(parentId, helperRoot);
        }

        if (args is ["--help"] or ["-h"])
        {
            PrintUsage();
            return 0;
        }

        var dryRun = args is ["--dry-run"];
        if (args.Length != 0 && !dryRun)
        {
            PrintUsage();
            return 2;
        }

        string repositoryRoot;
        string password;
        List<LaunchTarget> targets;
        try
        {
            repositoryRoot = FindRepositoryRoot();
            CleanupHelper.ReportPreviousError();
            password = await ReadTestDatabasePasswordAsync(repositoryRoot);
            targets = CreateTargets(repositoryRoot, password);
            ValidateTargets(targets);
        }
        catch (InvalidOperationException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }

        if (dryRun)
        {
            Console.WriteLine("Configuration is valid. The launcher would start:");
            Console.WriteLine("  Isolated SQL Server and RabbitMQ, then apply migrations and starter data");
            foreach (var target in targets)
            {
                Console.WriteLine($"  {target.Name}");
            }

            return 0;
        }

        var portConflicts = FindPortConflicts();
        if (portConflicts.Count > 0)
        {
            Console.Error.WriteLine("Cannot start the local test stack; these ports are already in use:");
            foreach (var conflict in portConflicts)
            {
                Console.Error.WriteLine($"  {conflict}");
            }

            Console.Error.WriteLine("Stop the existing services, then run the launcher again.");
            return 1;
        }

        using var shutdown = new CancellationTokenSource();
        var stopRequested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            shutdown.Cancel();
            stopRequested.TrySetResult();
        };
        Console.CancelKeyPress += cancelHandler;

        var processes = new List<(string Name, Process Process)>();
        WindowsProcessJob? job = null;
        Process? cleanupHelper = null;
        var result = 0;
        try
        {
            job = new WindowsProcessJob();
            cleanupHelper = await CleanupHelper.StartAsync(repositoryRoot, shutdown.Token);
            await BuildApisAsync(targets, repositoryRoot, job, shutdown.Token);
            await StartInfrastructureAsync(repositoryRoot, job, shutdown.Token);
            if (cleanupHelper.HasExited)
            {
                throw new InvalidOperationException("The test stack cleanup helper exited unexpectedly.");
            }

            Console.WriteLine("Starting APIs and browser clients. Press Ctrl+C to stop them.");

            foreach (var target in targets)
            {
                shutdown.Token.ThrowIfCancellationRequested();
                processes.Add((target.Name, StartTarget(target, job)));
            }

            Console.WriteLine("ChopChop: http://127.0.0.1:5174");
            Console.WriteLine("SQL Server: 127.0.0.1,1435; RabbitMQ: 127.0.0.1:5673");

            var processExits = processes
                .Select(async running =>
                {
                    await running.Process.WaitForExitAsync();
                    return running;
                })
                .ToArray();
            var processExit = Task.WhenAny(processExits);
            var helperExit = cleanupHelper.WaitForExitAsync();
            var completed = await Task.WhenAny(processExit, stopRequested.Task, helperExit);
            if (completed == processExit)
            {
                var exited = await await processExit;
                Console.Error.WriteLine(
                    $"{exited.Name} exited with code {exited.Process.ExitCode}. Stopping the remaining apps.");
                result = exited.Process.ExitCode == 0 ? 1 : exited.Process.ExitCode;
            }
            else if (completed == helperExit)
            {
                Console.Error.WriteLine("The test stack cleanup helper exited unexpectedly. Stopping the apps.");
                result = 1;
            }
            else
            {
                Console.WriteLine("Stopping local API and browser client processes...");
            }
        }
        catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
        {
            Console.WriteLine("Startup cancelled.");
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception or PlatformNotSupportedException or IOException)
        {
            Console.Error.WriteLine(exception.Message);
            result = 1;
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
            if (!await StopProcessesAsync(processes))
            {
                result = 1;
            }
            job?.Dispose();
            if (cleanupHelper is not null && !await CleanupHelper.StopAsync(cleanupHelper))
            {
                result = 1;
            }
        }

        return result;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage: dotnet run --project tools\\ChopChop.LocalLauncher [--dry-run]");
        Console.WriteLine("Starts the isolated test database, seeded data, all APIs, and the unified browser client.");
    }

    private static string FindRepositoryRoot()
    {
        foreach (var start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(start);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Solution.sln")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new InvalidOperationException("Could not locate the repository root containing Solution.sln.");
    }

    private static async Task<string> ReadTestDatabasePasswordAsync(string repositoryRoot)
    {
        var envFile = Path.Combine(repositoryRoot, ".env.test");
        if (!File.Exists(envFile))
        {
            throw new InvalidOperationException(
                "Missing .env.test. Copy .env.test.example to .env.test and set a local password first.");
        }

        using var process = new Process
        {
            StartInfo = CreateStartInfo(
                "docker",
                ["compose", "--project-name", ProjectName, "--env-file", envFile,
                    "-f", Path.Combine(repositoryRoot, "compose.test.yaml"), "config", "--format", "json"],
                repositoryRoot,
                null,
                redirectOutput: true)
        };
        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException("Could not start Docker Compose.");
            }
        }
        catch (System.ComponentModel.Win32Exception exception)
        {
            throw new InvalidOperationException("Could not start docker; ensure it is installed and on PATH.", exception);
        }

        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await Task.WhenAll(output, error, process.WaitForExitAsync());
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException("Docker Compose could not resolve the test configuration. Check .env.test.");
        }

        return ReadComposePassword(await output);
    }

    internal static string ReadComposePassword(string configuration)
    {
        string? password;
        try
        {
            using var document = JsonDocument.Parse(configuration);
            password = document.RootElement.GetProperty("services").GetProperty("sqlserver")
                .GetProperty("environment").GetProperty("MSSQL_SA_PASSWORD").GetString()
                ?.Replace("$$", "$", StringComparison.Ordinal);
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new InvalidOperationException("Docker Compose did not provide a valid test database password.", exception);
        }
        if (string.IsNullOrWhiteSpace(password) || password.StartsWith("ReplaceWith", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Set a strong local TEST_DB_PASSWORD in .env.test before starting.");
        }

        return password;
    }

    internal static List<LaunchTarget> CreateTargets(string repositoryRoot, string password)
    {
        var connectionStringBuilder = new DbConnectionStringBuilder
        {
            ["Server"] = "127.0.0.1,1435",
            ["Database"] = "ChopChopTest",
            ["User Id"] = "sa",
            ["Password"] = password,
            ["Encrypt"] = "True",
            ["TrustServerCertificate"] = "True"
        };
        var connectionString = connectionStringBuilder.ConnectionString;
        var rabbitUri = $"amqp://chopchop_test:{Uri.EscapeDataString(password)}@127.0.0.1:5673/";

        var apiEnvironment = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ASPNETCORE_ENVIRONMENT"] = DevelopmentEnvironment,
            ["Auth__DevelopmentMode"] = "true",
            ["ConnectionStrings__ChopChop"] = connectionString,
            ["ConnectionStrings__Equipment"] = connectionString,
            ["ConnectionStrings__Pantry"] = connectionString,
            ["Messaging__ConnectionString"] = rabbitUri,
            ["Messaging__Enabled"] = "true",
            ["RabbitMq__Enabled"] = "true",
            ["RabbitMq__Uri"] = rabbitUri,
            ["Logging__LogLevel__Microsoft.EntityFrameworkCore.Database.Command"] = "Warning",
            ["Serilog__MinimumLevel__Override__Microsoft.EntityFrameworkCore.Database.Command"] = "Warning",
            ["Client__Origin"] = "http://127.0.0.1:5174",
            ["Client__Origins__0"] = "http://127.0.0.1:5174",
            ["Client__Origins__1"] = "http://127.0.0.1:5175",
            ["Cors__AllowedOrigins__0"] = "http://127.0.0.1:5174",
            ["Cors__AllowedOrigins__1"] = "http://127.0.0.1:5175",
            ["Client__Url"] = "http://127.0.0.1:5174"
        };
        var mainClientEnvironment = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["VITE_DEV_AUTH"] = "true",
            ["VITE_AI_RECIPE_API_URL"] = "http://127.0.0.1:5081",
            ["VITE_INTERACTIVE_REVIEW_API_URL"] = "http://127.0.0.1:5082",
            ["VITE_RECIPE_MANAGEMENT_API_URL"] = "http://127.0.0.1:20858",
            ["VITE_COOKBOOK_API_URL"] = "http://127.0.0.1:20860",
            ["VITE_MEAL_PLANNER_API_URL"] = "http://127.0.0.1:5033",
            ["VITE_EQUIPMENT_API_URL"] = "http://127.0.0.1:5018",
            ["VITE_PANTRY_API_URL"] = "http://127.0.0.1:5131"
        };

        return
        [
            Api("AI Recipe API", "AiRecipeGeneration.Api", "http://127.0.0.1:5081"),
            Api("Interactive Review API", "InteractiveReview.Api", "http://127.0.0.1:5082"),
            Api("Recipe Management API", "RecipeManagement.Api", "http://127.0.0.1:20858"),
            Api("Cookbook API", "Cookbook.Api", "http://127.0.0.1:20860"),
            Api("Equipment API", "Equipment.Api", "http://127.0.0.1:5018"),
            Api("Pantry API", "Pantry.Api", "http://127.0.0.1:5131"),
            Api("Meal Planner API", "MealPlanner.Api", "http://127.0.0.1:5033"),
            Api("User Service API", "UserService.Api", "http://127.0.0.1:5199"),
            Client("ChopChop browser client", Path.Combine("src", "ChopChop.Client"), 5174, mainClientEnvironment)
        ];

        LaunchTarget Api(string name, string project, string url) =>
            new(
                name,
                "dotnet",
                [
                    "run",
                    "--no-build",
                    "--no-launch-profile",
                    "--project",
                    Path.Combine(repositoryRoot, "src", project, $"{project}.csproj"),
                    "--",
                    "--urls",
                    url
                ],
                repositoryRoot,
                apiEnvironment);

        LaunchTarget Client(
            string name,
            string relativeDirectory,
            int port,
            IReadOnlyDictionary<string, string> environment)
        {
            var clientDirectory = Path.Combine(repositoryRoot, relativeDirectory);
            return new LaunchTarget(
                name,
                "node",
                [
                    Path.Combine(clientDirectory, "node_modules", "vite", "bin", "vite.js"),
                    "--host",
                    "127.0.0.1",
                    "--port",
                    port.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    "--strictPort"
                ],
                clientDirectory,
                environment);
        }
    }

    private static void ValidateTargets(IEnumerable<LaunchTarget> targets)
    {
        foreach (var target in targets)
        {
            if (target.FileName == "node" && (target.Arguments.Count == 0 || !File.Exists(target.Arguments[0])))
            {
                var clientDirectory = target.WorkingDirectory;
                throw new InvalidOperationException(
                    $"Vite dependencies are missing for {target.Name}. Run `npm ci` in {clientDirectory} first.");
            }

            if (target.FileName == "dotnet")
            {
                const int projectPathIndex = 4;
                if (target.Arguments.Count <= projectPathIndex || !File.Exists(target.Arguments[projectPathIndex]))
                {
                    throw new InvalidOperationException($"Could not find the API project for {target.Name}.");
                }
            }
        }
    }

    private static List<string> FindPortConflicts()
    {
        var conflicts = new List<string>();
        foreach (var port in new[] { 5174, 5018, 5033, 5081, 5082, 5131, 5199, 20858, 20860 })
        {
            using var listener = new TcpListener(IPAddress.Loopback, port);
            try
            {
                listener.Start();
            }
            catch (SocketException)
            {
                conflicts.Add($"127.0.0.1:{port}");
                continue;
            }

            listener.Stop();
        }

        return conflicts;
    }

    private static async Task StartInfrastructureAsync(string repositoryRoot, WindowsProcessJob job, CancellationToken cancellationToken)
    {
        var composeFile = Path.Combine(repositoryRoot, "compose.test.yaml");
        var envFile = Path.Combine(repositoryRoot, ".env.test");
        var commonArguments = new[]
        {
            "compose",
            "--project-name", ProjectName,
            "--env-file", envFile,
            "-f", composeFile
        };

        await RunCommandAsync(
            "Start isolated SQL Server and RabbitMQ",
            "docker",
            [.. commonArguments, "up", "-d", "--wait", "sqlserver", "rabbitmq"],
            repositoryRoot,
            job,
            cancellationToken);
        await RunCommandAsync(
            "Apply migrations and seed starter data",
            "docker",
            [.. commonArguments, "run", "--build", "--rm", "seed"],
            repositoryRoot,
            job,
            cancellationToken);
    }

    private static async Task BuildApisAsync(
        IEnumerable<LaunchTarget> targets,
        string repositoryRoot,
        WindowsProcessJob job,
        CancellationToken cancellationToken)
    {
        foreach (var target in targets.Where(target => target.FileName == "dotnet"))
        {
            var projectPath = target.Arguments[4];
            await RunCommandAsync(
                $"Build {target.Name}",
                "dotnet",
                ["build", projectPath],
                repositoryRoot,
                job,
                cancellationToken);
        }
    }

    private static async Task RunCommandAsync(
        string name,
        string fileName,
        IEnumerable<string> arguments,
        string workingDirectory,
        WindowsProcessJob job,
        CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = CreateStartInfo(fileName, arguments, workingDirectory, null, redirectOutput: true)
        };
        process.OutputDataReceived += (_, eventArgs) => WriteProcessLine(name, eventArgs.Data);
        process.ErrorDataReceived += (_, eventArgs) => WriteProcessLine(name, eventArgs.Data);

        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException($"Could not start command for: {name}.");
            }
        }
        catch (System.ComponentModel.Win32Exception exception)
        {
            throw new InvalidOperationException($"Could not start {fileName}; ensure it is installed and on PATH.", exception);
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        AssignToJob(process, job);
        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }

            throw;
        }
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"{name} failed with exit code {process.ExitCode}.");
        }
    }

    private static Process StartTarget(LaunchTarget target, WindowsProcessJob job)
    {
        var process = new Process
        {
            StartInfo = CreateStartInfo(
                target.FileName,
                target.Arguments,
                target.WorkingDirectory,
                target.Environment,
                redirectOutput: true)
        };
        process.OutputDataReceived += (_, eventArgs) => WriteProcessLine(target.Name, eventArgs.Data);
        process.ErrorDataReceived += (_, eventArgs) => WriteProcessLine(target.Name, eventArgs.Data);

        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException($"Could not start {target.Name}.");
            }
        }
        catch (System.ComponentModel.Win32Exception exception)
        {
            process.Dispose();
            throw new InvalidOperationException(
                $"Could not start {target.Name}; ensure {target.FileName} is installed and on PATH.",
                exception);
        }

        AssignToJob(process, job);
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        Console.WriteLine($"Started {target.Name}.");
        return process;
    }

    private static void AssignToJob(Process process, WindowsProcessJob job)
    {
        try
        {
            job.Add(process);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
            }

            throw;
        }
    }

    private static ProcessStartInfo CreateStartInfo(
        string fileName,
        IEnumerable<string> arguments,
        string workingDirectory,
        IReadOnlyDictionary<string, string>? environment,
        bool redirectOutput)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = redirectOutput,
            RedirectStandardError = redirectOutput,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (environment is not null)
        {
            foreach (var item in environment)
            {
                startInfo.Environment[item.Key] = item.Value;
            }
        }

        return startInfo;
    }

    private static void WriteProcessLine(string name, string? line)
    {
        if (line is not null)
        {
            Console.WriteLine($"[{name}] {line}");
        }
    }

    private static async Task<bool> StopProcessesAsync(IEnumerable<(string Name, Process Process)> processes)
    {
        var success = true;
        foreach (var running in processes)
        {
            try
            {
                if (!running.Process.HasExited)
                {
                    running.Process.Kill(entireProcessTree: true);
                    await running.Process.WaitForExitAsync();
                }
            }
            catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                Console.Error.WriteLine($"Could not stop {running.Name}: {exception.Message}");
                success = false;
            }
            finally
            {
                running.Process.Dispose();
            }
        }

        return success;
    }

    internal sealed record LaunchTarget(
        string Name,
        string FileName,
        IReadOnlyList<string> Arguments,
        string WorkingDirectory,
        IReadOnlyDictionary<string, string> Environment);
}
