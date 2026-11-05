// Launches every ChopChop API in Development. Run from the repository root: dotnet run tools/RunAll.cs
using System.Diagnostics;

var root = Directory.GetCurrentDirectory();
(string Project, int Port)[] apis =
[
    ("RecipeManagement.Api", 20858),
    ("Cookbook.Api", 20860),
    ("UserService.Api", 5199),
    ("MealPlanner.Api", 5033),
    ("AiRecipeGeneration.Api", 5081),
    ("InteractiveReview.Api", 5082),
];

var processes = new List<Process>();
var outputLock = new object();

void Forward(string name, string? line)
{
    if (line is null) return;
    lock (outputLock) Console.WriteLine($"[{name}] {line}");
}

foreach (var (project, port) in apis)
{
    var projectPath = Path.Combine(root, "src", project);
    if (!Directory.Exists(projectPath))
    {
        Console.Error.WriteLine($"Run from the repository root; {projectPath} was not found.");
        return 1;
    }

    var startInfo = new ProcessStartInfo("dotnet", $"run --project \"{projectPath}\" --no-launch-profile")
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
    };
    startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
    startInfo.Environment["ASPNETCORE_URLS"] = $"http://localhost:{port}";

    var process = new Process { StartInfo = startInfo };
    process.OutputDataReceived += (_, e) => Forward(project, e.Data);
    process.ErrorDataReceived += (_, e) => Forward(project, e.Data);
    process.Start();
    process.BeginOutputReadLine();
    process.BeginErrorReadLine();
    processes.Add(process);
    Console.WriteLine($"Started {project} on http://localhost:{port}");
}

using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    shutdown.Cancel();
};

try
{
    await Task.WhenAll(processes.Select(p => p.WaitForExitAsync(shutdown.Token)));
}
catch (OperationCanceledException)
{
}

foreach (var process in processes.Where(p => !p.HasExited))
    process.Kill(entireProcessTree: true);

return 0;
