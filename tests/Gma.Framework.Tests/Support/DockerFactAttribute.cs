namespace Gma.Framework.Tests.Support;

using System.Diagnostics;
using Xunit;

[AttributeUsage(AttributeTargets.Method)]
internal sealed class DockerFactAttribute : FactAttribute
{
    public DockerFactAttribute()
    {
        if (!DockerAvailability.IsRequired && !DockerAvailability.IsAvailable)
        {
            this.Skip = "Docker is not available; container-backed integration scenarios were not executed.";
        }
    }

    private static class DockerAvailability
    {
        private static readonly Lazy<bool> Available = new(Check);

        public static bool IsAvailable => Available.Value;

        public static bool IsRequired => string.Equals(
            Environment.GetEnvironmentVariable("GMA_REQUIRE_DOCKER_TESTS"),
            "true",
            StringComparison.OrdinalIgnoreCase);

        private static bool Check()
        {
            try
            {
                using Process process = new()
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "docker",
                        CreateNoWindow = true,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true,
                        UseShellExecute = false
                    }
                };
                process.StartInfo.ArgumentList.Add("info");
                process.Start();
                if (!process.WaitForExit(TimeSpan.FromSeconds(10)))
                {
                    TryKill(process);
                    return false;
                }

                return process.ExitCode == 0;
            }
            catch (Exception exception) when (
                exception is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                return false;
            }
        }

        private static void TryKill(Process process)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (Exception exception) when (
                exception is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
            }
        }
    }
}
