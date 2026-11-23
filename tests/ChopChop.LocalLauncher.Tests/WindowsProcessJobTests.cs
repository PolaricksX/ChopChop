using System.Diagnostics;

namespace ChopChop.LocalLauncher.Tests;

public sealed class WindowsProcessJobTests
{
    [Fact]
    public async Task DisposingJobStopsAssignedProcess()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("cmd.exe")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                ArgumentList = { "/c", "ping -n 60 127.0.0.1 > nul" }
            }
        };
        Assert.True(process.Start());
        try
        {
            using (var job = new WindowsProcessJob())
            {
                job.Add(process);
                Assert.False(process.HasExited);
            }

            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(process.HasExited);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
        }
    }
}
