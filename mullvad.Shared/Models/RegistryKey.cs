using System.Collections.Generic;

namespace mullvad.Models
{
    public sealed class RegistryKey
    {
        public string KeyPath    { get; set; } = string.Empty;
        public bool   HasSubKeys { get; set; }
        public List<RegistryValue> Values { get; set; } = new List<RegistryValue>();
    }
}
