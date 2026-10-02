using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Dependencies.General
{
    public static class IpApi
    {
        private static string _cachedIp;

        private static readonly object _lock = new object();

        public static string GetPublicIp()
        {
            if (!string.IsNullOrEmpty(_cachedIp))
            {
                return _cachedIp;
            }
            lock (_lock)
            {
                if (!string.IsNullOrEmpty(_cachedIp))
                {
                    return _cachedIp;
                }
                try
                {
                    using (WebClient webClient = new WebClient())
                    {
                        string text = webClient.DownloadString("http://icanhazip.com");
                        if (!string.IsNullOrEmpty(text))
                        {
                            _cachedIp = text.Trim();
                        }
                    }
                }
                catch
                {
                    _cachedIp = "Request failed";
                }
                return _cachedIp;
            }
        }
    }
}
