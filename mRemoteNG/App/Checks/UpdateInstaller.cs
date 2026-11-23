using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Runtime.Versioning;
using System.Text;

namespace mRemoteNG.App.Update
{
    /// <summary>
    /// Installs a downloaded update after the current process has exited.
    ///
    /// A running mRemoteNG.exe cannot overwrite itself, so the downloaded zip is unpacked to a
    /// staging folder and a small PowerShell helper waits for this process to end, copies the new
    /// files over the installation folder and starts the program again.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static class UpdateInstaller
    {
        private static readonly string UpdateRoot =
            Path.Combine(Path.GetTempPath(), "mRemoteNG");

        /// <summary>
        /// Starts the installation of the given update file. Called from the shutdown path.
        /// </summary>
        public static void ApplyOnExit(string updateFilePath)
        {
            if (string.IsNullOrEmpty(updateFilePath) || !File.Exists(updateFilePath))
                return;

            if (!updateFilePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                // Anything else (for example an .msi) is still handed over to the shell.
                Process.Start(new ProcessStartInfo(updateFilePath) { UseShellExecute = true });
                return;
            }

            string stagingDirectory = Path.Combine(UpdateRoot, "staging-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stagingDirectory);
            ZipFile.ExtractToDirectory(updateFilePath, stagingDirectory);

            string targetDirectory = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            string executablePath = Path.Combine(targetDirectory, "mRemoteNG.exe");
            string logPath = Path.Combine(UpdateRoot, "update.log");
            string scriptPath = Path.Combine(UpdateRoot, $"apply-update-{Guid.NewGuid():N}.ps1");

            File.WriteAllText(scriptPath,
                              BuildScript(Environment.ProcessId, stagingDirectory, targetDirectory, executablePath, logPath, updateFilePath),
                              new UTF8Encoding(false));

            ProcessStartInfo startInfo = new("powershell.exe")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-ExecutionPolicy");
            startInfo.ArgumentList.Add("Bypass");
            startInfo.ArgumentList.Add("-WindowStyle");
            startInfo.ArgumentList.Add("Hidden");
            startInfo.ArgumentList.Add("-File");
            startInfo.ArgumentList.Add(scriptPath);

            Process.Start(startInfo);
        }

        /// <summary>
        /// Creates the PowerShell helper that swaps the files once this process exited.
        /// Public so the generated script can be inspected and tested.
        /// </summary>
        public static string BuildScript(int processId, string stagingDirectory, string targetDirectory,
                                         string executablePath, string logPath, string updateFilePath)
        {
            return $$"""
                      $ErrorActionPreference = 'Stop'
                      $log = {{Quote(logPath)}}
                      function Write-Log($message) {
                          Add-Content -LiteralPath $log -Value ("{{DateTime.Now:yyyy-MM-dd HH:mm:ss}} " + $message)
                      }

                      try {
                          Write-Log "Waiting for mRemoteNG (PID {{processId}}) to exit"
                          $process = Get-Process -Id {{processId}} -ErrorAction SilentlyContinue
                          if ($process) { $null = $process.WaitForExit(180000) }
                          Start-Sleep -Milliseconds 1000

                          Write-Log ("Copying update from " + {{Quote(stagingDirectory)}} + " to " + {{Quote(targetDirectory)}})
                          Get-ChildItem -LiteralPath {{Quote(stagingDirectory)}} -Force | ForEach-Object {
                              Copy-Item -LiteralPath $_.FullName -Destination {{Quote(targetDirectory)}} -Recurse -Force
                          }

                          Write-Log ("Starting " + {{Quote(executablePath)}})
                          Start-Process -FilePath {{Quote(executablePath)}}
                          Write-Log "Update applied"
                      } catch {
                          Write-Log ("Update failed: " + $_)
                          Add-Content -LiteralPath $log -Value $_.ScriptStackTrace
                      } finally {
                          Remove-Item -LiteralPath {{Quote(stagingDirectory)}} -Recurse -Force -ErrorAction SilentlyContinue
                          Remove-Item -LiteralPath {{Quote(updateFilePath)}} -Force -ErrorAction SilentlyContinue
                          Remove-Item -LiteralPath $PSCommandPath -Force -ErrorAction SilentlyContinue
                      }
                      """;
        }

        private static string Quote(string value) => "'" + value.Replace("'", "''") + "'";
    }
}
