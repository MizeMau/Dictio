using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace Dictio.TTS
{
    /// <summary>
    /// Launches the Chatterbox Turbo Python server as a child process using the
    /// .venv that lives inside the TTS folder, then waits until /health responds.
    /// Killed automatically on Dispose().
    ///
    /// Expected layout (relative to the .exe):
    ///   TTS/
    ///     .venv/Scripts/python.exe   ← venv Python
    ///     tts_server.py
    ///     voice.wav
    /// </summary>
    internal sealed class ServerManager : IDisposable
    {
        // ── Layout constants ────────────────────────────────────────────────────
        private const string TtsFolder = "TTS";
        private const string ServerScript = "tts_server.py";
        private const string VenvPython = @".venv\Scripts\python.exe";   // Windows

        // ── Server constants ────────────────────────────────────────────────────
        private const string Host = "127.0.0.1";
        private const int Port = 7860;
        private const int StartupTimeoutMs = 180_000;  // 3 min — first run downloads weights
        private const int PollIntervalMs = 1_500;

        public string BaseUrl { get; } = $"http://{Host}:{Port}";

        private Process? _process;
        private bool _disposed;

        // -----------------------------------------------------------------------
        // Start
        // -----------------------------------------------------------------------
        public async Task StartAsync(CancellationToken ct = default)
        {
            KillPortSquatter(Port);

            string ttsDir = ResolveTtsFolder();
            string pythonExe = Path.Combine(ttsDir, VenvPython);
            string scriptPath = Path.Combine(@"C:\Users\Mize\Project\Dictio\Dictio\TTS", ServerScript);

            if (!File.Exists(pythonExe))
                throw new FileNotFoundException(
                    $"Venv Python not found. Expected: {pythonExe}");

            if (!File.Exists(scriptPath))
                throw new FileNotFoundException(
                    $"Server script not found. Expected: {scriptPath}");

            Log($"Python : {pythonExe}");
            Log($"Script : {scriptPath}");

            var psi = new ProcessStartInfo
            {
                FileName = pythonExe,
                Arguments = $"\"{scriptPath}\"",
                WorkingDirectory = ttsDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            _process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _process.OutputDataReceived += (_, e) => PrintPy(e.Data);
            _process.ErrorDataReceived += (_, e) => PrintPy(e.Data);

            _process.Start();
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();

            await WaitForHealthAsync(ct);
        }

        public bool IsRunning => _process is { HasExited: false };

        // -----------------------------------------------------------------------
        // Kill whatever already sits on the port
        // -----------------------------------------------------------------------
        private static void KillPortSquatter(int port)
        {
            try
            {
                var psi = new ProcessStartInfo("netstat", "-ano")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                using var ns = Process.Start(psi)!;
                string output = ns.StandardOutput.ReadToEnd();
                ns.WaitForExit();

                var pattern = new Regex(
                    $@"TCP\s+[\d.]+:{port}\s+[\d.:]+\s+\w+\s+(\d+)",
                    RegexOptions.Multiline);

                var pids = pattern.Matches(output)
                                  .Select(m => int.Parse(m.Groups[1].Value))
                                  .Where(pid => pid > 0)
                                  .Distinct()
                                  .ToList();

                foreach (int pid in pids)
                {
                    try
                    {
                        using var victim = Process.GetProcessById(pid);
                        Log($"Killing stale process on port {port} (PID {pid}: {victim.ProcessName})");
                        victim.Kill(entireProcessTree: true);
                        victim.WaitForExit(3_000);
                    }
                    catch { /* already gone */ }
                }

                if (pids.Count > 0)
                    Thread.Sleep(600); // let OS release the socket
            }
            catch (Exception ex)
            {
                Log($"Port check skipped: {ex.Message}");
            }
        }

        // -----------------------------------------------------------------------
        // Poll /health until the server is ready
        // -----------------------------------------------------------------------
        private async Task WaitForHealthAsync(CancellationToken ct)
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var healthUrl = $"{BaseUrl}/health";
            var deadline = DateTime.UtcNow.AddMilliseconds(StartupTimeoutMs);

            Console.Write("[Server] Waiting for Chatterbox Turbo");

            while (DateTime.UtcNow < deadline)
            {
                ct.ThrowIfCancellationRequested();

                if (_process is { HasExited: true })
                    throw new InvalidOperationException(
                        "[Server] Python process exited unexpectedly. " +
                        "Check that the .venv packages are installed.");

                try
                {
                    var resp = await http.GetAsync(healthUrl, ct);
                    if (resp.IsSuccessStatusCode)
                    {
                        Console.WriteLine(" ✓");
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("[Server] Chatterbox Turbo is ready.");
                        Console.ResetColor();
                        return;
                    }
                }
                catch { /* not up yet */ }

                Console.Write(".");
                await Task.Delay(PollIntervalMs, ct);
            }

            throw new TimeoutException(
                "[Server] Timed out waiting for Chatterbox Turbo to start.");
        }

        // -----------------------------------------------------------------------
        // Resolve TTS/ folder — works both from IDE (repo root) and published exe
        // -----------------------------------------------------------------------
        private static string ResolveTtsFolder()
        {
            return @"C:\Users\Mize\Project\Dictio\TTS";

            //// Walk up from the exe directory until we find a TTS/ subfolder
            //var dir = new DirectoryInfo(AppContext.BaseDirectory);
            //while (dir != null)
            //{
            //    string candidate = Path.Combine(dir.FullName, TtsFolder);
            //    if (Directory.Exists(candidate)) return candidate;
            //    dir = dir.Parent;
            //}

            //throw new DirectoryNotFoundException(
            //    $"Cannot find the '{TtsFolder}' folder. " +
            //    $"Expected it somewhere above: {AppContext.BaseDirectory}");
        }

        // -----------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------
        private static void Log(string msg)
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"[Server] {msg}");
            Console.ResetColor();
        }

        private static void PrintPy(string? line)
        {
            if (string.IsNullOrEmpty(line)) return;
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"  [py] {line}");
            Console.ResetColor();
        }

        // -----------------------------------------------------------------------
        // IDisposable
        // -----------------------------------------------------------------------
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_process is { HasExited: false })
            {
                try { _process.Kill(entireProcessTree: true); _process.WaitForExit(3_000); }
                catch { /* best effort */ }
            }

            _process?.Dispose();
        }
    }

}
