using System;
using System.IO;
using System.Net;
using System.Text;

namespace mullvad.Module.GeoLocation
{
    public sealed class GeoLocationModule
    {
        public static string ModuleId => "mullvad.geolocation";

        public string Execute(string action, string payload)
        {
            try
            {
                switch (action)
                {
                    case "geolocate": return Geolocate();
                    case "advanced":  return Advanced();
                    default:          return Err("unknown action: " + action);
                }
            }
            catch (Exception ex) { return Err(ex.Message); }
        }

        // Basic: all ip-api.com fields — country, city, ISP, lat/lon, timezone, etc.
        private static string Geolocate()
        {
            const string url =
                "http://ip-api.com/json/?fields=status,message,continent,continentCode," +
                "country,countryCode,region,regionName,city,district,zip,lat,lon," +
                "timezone,offset,currency,isp,org,as,asname,reverse,mobile,proxy,hosting,query";
            return HttpGet(url, 10000);
        }

        // Advanced: ip-api.com + ipapi.co combined for maximum data
        private static string Advanced()
        {
            const string url1 =
                "http://ip-api.com/json/?fields=status,message,continent,continentCode," +
                "country,countryCode,region,regionName,city,district,zip,lat,lon," +
                "timezone,offset,currency,isp,org,as,asname,reverse,mobile,proxy,hosting,query";

            string ipapi = HttpGet(url1, 10000);

            string ipapico = "{}";
            try { ipapico = HttpGet("https://ipapi.co/json/", 8000); } catch { }

            return "{\"ipapi\":" + ipapi + ",\"ipapico\":" + ipapico + "}";
        }

        private static string HttpGet(string url, int timeoutMs)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Timeout   = timeoutMs;
            req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)";
            using var resp   = (HttpWebResponse)req.GetResponse();
            using var reader = new StreamReader(resp.GetResponseStream(), Encoding.UTF8);
            return reader.ReadToEnd();
        }

        private static string Err(string msg) =>
            "{\"error\":\"" + msg.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"}";
    }
}
