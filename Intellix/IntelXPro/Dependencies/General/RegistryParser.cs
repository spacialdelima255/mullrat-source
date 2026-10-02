using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Dependencies.General
{
    public static class RegistryParser
    {
        public static List<string> ParseKey(RegistryKey key)
        {
            List<string> list = new List<string>();
            if (key == null)
            {
                return list;
            }
            string[] valueNames = key.GetValueNames();
            foreach (string text in valueNames)
            {
                object value = key.GetValue(text);
                string text2;
                switch (key.GetValueKind(text))
                {
                    case RegistryValueKind.Binary:
                        text2 = ((!(value is byte[] array)) ? "null" : BitConverter.ToString(array).Replace("-", ""));
                        break;
                    case RegistryValueKind.DWord:
                    case RegistryValueKind.QWord:
                        text2 = value.ToString();
                        break;
                    case RegistryValueKind.String:
                    case RegistryValueKind.ExpandString:
                        text2 = value?.ToString() ?? "null";
                        break;
                    case RegistryValueKind.MultiString:
                        text2 = ((!(value is string[] value2)) ? "null" : string.Join(", ", value2));
                        break;
                    default:
                        text2 = value?.ToString() ?? "null";
                        break;
                }
                list.Add(text + ": " + text2);
            }
            return list;
        }
    }
}
