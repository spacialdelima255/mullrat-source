using System.Reflection;

namespace mullvad.Theme
{
    internal static class IconLoader
    {
        private const string Prefix     = "mullvad.Resources.icons.";
        private const string FlagPrefix = "mullvad.Resources.icons.flags.";

        private static readonly Assembly Asm = Assembly.GetExecutingAssembly();

        public static Image? Load(string filename)
        {
            try
            {
                using var stream = Asm.GetManifestResourceStream(Prefix + filename);
                if (stream is null) return null;
                var ms = new MemoryStream(new BinaryReader(stream).ReadBytes((int)stream.Length));
                if (filename.EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
                    return new Icon(ms).ToBitmap();
                return Image.FromStream(ms);
            }
            catch { return null; }
        }

        public static Image? LoadFlag(string countryCode)
        {
            try
            {
                using var stream = Asm.GetManifestResourceStream(FlagPrefix + countryCode.ToLower() + ".png");
                if (stream is null) return null;
                var ms = new MemoryStream(new BinaryReader(stream).ReadBytes((int)stream.Length));
                return Image.FromStream(ms);
            }
            catch { return null; }
        }

        public static void SetIcon(ToolStripMenuItem item, string filename)
        {
            try { item.Image = Load(filename); }
            catch { }
        }
    }
}
