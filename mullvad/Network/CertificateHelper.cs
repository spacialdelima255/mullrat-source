using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace mullvad.Network
{
    public static class CertificateHelper
    {
        private const string CertFile = "mullvad_server.pfx";
        private const string CertPass = "mullvad_internal_2024";

        public static X509Certificate2 GetOrCreate()
        {
            if (File.Exists(CertFile))
            {
                try { return new X509Certificate2(CertFile, CertPass); }
                catch { }
            }

            using var rsa = RSA.Create(2048);
            var req  = new CertificateRequest("CN=mullvad-server", rsa,
                HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            var cert = req.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddDays(-1),
                DateTimeOffset.UtcNow.AddYears(10));

            var pfxBytes = cert.Export(X509ContentType.Pfx, CertPass);
            File.WriteAllBytes(CertFile, pfxBytes);

            return new X509Certificate2(pfxBytes, CertPass,
                X509KeyStorageFlags.Exportable);
        }
    }
}
