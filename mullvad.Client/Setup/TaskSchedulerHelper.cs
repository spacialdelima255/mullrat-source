using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using mullvad.Client.Config;

namespace mullvad.Client.Setup
{
    public static class TaskSchedulerHelper
    {
        public const int TRIGGER_LOGON            = 0;
        public const int TRIGGER_STARTUP          = 1;
        public const int TRIGGER_WORKSTATION_LOCK = 2;
        public const int TRIGGER_INTERVAL         = 3;

        public static void RegisterTask(string exePath)
        {
            try
            {
                RemoveTask(Settings.TaskName);

                string xml     = BuildTaskXml(exePath);
                string xmlPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".xml");

                File.WriteAllText(xmlPath, xml, Encoding.Unicode);
                int code;
                try   { code = RunSchtasks("/Create /F /XML \"" + xmlPath + "\" /TN \"" + Settings.TaskName + "\""); }
                finally { try { File.Delete(xmlPath); } catch { } }

                if (code != 0) RegisterTaskFallback(exePath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("TaskScheduler XML register failed: " + ex.Message);
                RegisterTaskFallback(exePath);
            }
        }

        private static void RegisterTaskFallback(string exePath)
        {
            try
            {
                string escaped = "\"" + exePath.Replace("\"", "\\\"") + "\"";
                string trigger = BuildFallbackTrigger();
                string args    = "/Create /F /TN \"" + Settings.TaskName + "\" /TR " + escaped + trigger;
                if (Settings.TaskHighestPrivileges) args += " /RL HIGHEST";
                if (Settings.TaskLoggedOffRun)      args += " /RU SYSTEM";
                RunSchtasks(args);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("TaskScheduler fallback failed: " + ex.Message); }
        }

        private static string BuildFallbackTrigger()
        {
            switch (Settings.TaskTrigger)
            {
                case TRIGGER_STARTUP:          return " /SC ONSTART";
                case TRIGGER_WORKSTATION_LOCK: return " /SC ONLOGON";
                case TRIGGER_INTERVAL:
                    int mins = Math.Max(1, Settings.TaskInterval);
                    return " /SC MINUTE /MO " + mins + " /ST 00:00";
                default: return " /SC ONLOGON";
            }
        }

        public static void RemoveTask(string taskName)
        {
            try { RunSchtasks("/Delete /TN \"" + taskName + "\" /F"); }
            catch { }
        }

        private static string BuildTaskXml(string exePath)
        {
            string escapedPath = exePath.Replace("&", "&amp;").Replace("<", "&lt;")
                                        .Replace(">", "&gt;").Replace("\"", "&quot;");
            return "<?xml version=\"1.0\" encoding=\"UTF-16\"?>\r\n" +
                   "<Task version=\"1.2\" xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\">\r\n" +
                   "  <Triggers>\r\n" + BuildTriggerXml() + "  </Triggers>\r\n" +
                   "  <Principals>\r\n" + BuildPrincipalXml() + "  </Principals>\r\n" +
                   "  <Settings>\r\n" + BuildSettingsXml() + "  </Settings>\r\n" +
                   "  <Actions Context=\"Author\">\r\n" +
                   "    <Exec>\r\n" +
                   "      <Command>" + escapedPath + "</Command>\r\n" +
                   "    </Exec>\r\n" +
                   "  </Actions>\r\n" +
                   "</Task>";
        }

        private static string BuildTriggerXml()
        {
            switch (Settings.TaskTrigger)
            {
                case TRIGGER_STARTUP:
                    return "    <BootTrigger>\r\n      <Enabled>true</Enabled>\r\n    </BootTrigger>\r\n";
                case TRIGGER_WORKSTATION_LOCK:
                    return "    <SessionStateChangeTrigger>\r\n      <Enabled>true</Enabled>\r\n" +
                           "      <StateChange>SessionLock</StateChange>\r\n    </SessionStateChangeTrigger>\r\n";
                case TRIGGER_INTERVAL:
                    int mins = Math.Max(1, Settings.TaskInterval);
                    string t = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
                    return "    <TimeTrigger>\r\n" +
                           "      <StartBoundary>" + t + "</StartBoundary>\r\n" +
                           "      <Enabled>true</Enabled>\r\n" +
                           "      <Repetition>\r\n" +
                           "        <Interval>PT" + mins + "M</Interval>\r\n" +
                           "        <StopAtDurationEnd>false</StopAtDurationEnd>\r\n" +
                           "      </Repetition>\r\n" +
                           "    </TimeTrigger>\r\n";
                default:
                    return "    <LogonTrigger>\r\n      <Enabled>true</Enabled>\r\n    </LogonTrigger>\r\n";
            }
        }

        private static string BuildPrincipalXml()
        {
            string runLevel = Settings.TaskHighestPrivileges ? "HighestAvailable" : "LeastPrivilege";
            if (Settings.TaskLoggedOffRun)
            {
                return "    <Principal id=\"Author\">\r\n" +
                       "      <UserId>S-1-5-18</UserId>\r\n" +
                       "      <RunLevel>" + runLevel + "</RunLevel>\r\n" +
                       "    </Principal>\r\n";
            }
            string userId = (Environment.UserDomainName + "\\" + Environment.UserName)
                .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
            return "    <Principal id=\"Author\">\r\n" +
                   "      <UserId>" + userId + "</UserId>\r\n" +
                   "      <LogonType>InteractiveToken</LogonType>\r\n" +
                   "      <RunLevel>" + runLevel + "</RunLevel>\r\n" +
                   "    </Principal>\r\n";
        }

        private static string BuildSettingsXml()
        {
            var sb = new StringBuilder();
            sb.AppendLine("    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>");
            sb.AppendLine("    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>");
            sb.AppendLine("    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>");
            sb.AppendLine("    <Hidden>" + (Settings.TaskHidden ? "true" : "false") + "</Hidden>");
            sb.AppendLine("    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>");
            sb.AppendLine("    <Priority>7</Priority>");
            if (Settings.TaskRestartOnFailure)
            {
                sb.AppendLine("    <RestartOnFailure>");
                sb.AppendLine("      <Interval>PT1M</Interval>");
                sb.AppendLine("      <Count>999</Count>");
                sb.AppendLine("    </RestartOnFailure>");
            }
            return sb.ToString();
        }

        private static int RunSchtasks(string arguments)
        {
            using (var p = new Process())
            {
                p.StartInfo = new ProcessStartInfo
                {
                    FileName        = "schtasks.exe",
                    Arguments       = arguments,
                    CreateNoWindow  = true,
                    UseShellExecute = false,
                    WindowStyle     = ProcessWindowStyle.Hidden,
                };
                p.Start();
                return p.WaitForExit(15000) ? p.ExitCode : -1;
            }
        }
    }
}
