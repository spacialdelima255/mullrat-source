using System;
using System.IO;
using System.Reflection;

namespace mullvad.Client
{
    // Entry point — kept free of any mullvad.shared / Newtonsoft.Json type references
    // so the JIT can compile Main() before those assemblies are loaded.
    internal static class Loader
    {
        internal static void Main(string[] args)
        {
            Trace("STARTUP — Main() reached");

            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                Trace("UNHANDLED_EXCEPTION — " + e.ExceptionObject);

            AppDomain.CurrentDomain.AssemblyResolve += OnResolve;
            Trace("AssemblyResolve registered");

            try
            {
                RunClient(args);
            }
            catch (Exception ex)
            {
                Trace("CRASH in RunClient — " + ex);
            }

            Trace("Main() exiting");
        }

        private static void RunClient(string[] args)
        {
            ClientMain.Run(args);
        }

        internal static void Trace(string message)
        {
            var line = DateTime.Now.ToString("[HH:mm:ss.fff] ") + message + "\r\n";
            try
            {
                File.AppendAllText(
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log.txt"),
                    line);
            }
            catch { }
            try
            {
                File.AppendAllText(
                    Path.Combine(Path.GetTempPath(), "mullvad_client.txt"),
                    line);
            }
            catch { }
        }

        private static Assembly? OnResolve(object sender, ResolveEventArgs e)
        {
            var name = new AssemblyName(e.Name).Name + ".dll";
            Trace("AssemblyResolve — " + name);
            using var stream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream(name);
            if (stream == null)
            {
                Trace("AssemblyResolve — NOT FOUND: " + name);
                return null;
            }
            var bytes = new byte[stream.Length];
            _ = stream.Read(bytes, 0, bytes.Length);
            Trace("AssemblyResolve — loaded " + name + " (" + bytes.Length + " bytes)");
            return Assembly.Load(bytes);
        }
    }
}
