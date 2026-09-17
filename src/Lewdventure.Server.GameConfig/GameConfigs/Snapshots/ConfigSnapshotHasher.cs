using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Server.GameConfigs
{
    internal sealed class ConfigSnapshotHasher
    {
        public const string VersionPrefix = "sha256:";

        private const string Header = "lewdventure-config-snapshot/v1\n";

        public string ComputeVersion(IReadOnlyList<ConfigSnapshotDomain> domains)
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

            Append(hash, Header);

            for (int i = 0; i < domains.Count; i++)
            {
                var domain = domains[i];

                Append(hash, domain.Domain);
                Append(hash, "\n");
                Append(hash, domain.RowsJson.Length.ToString(CultureInfo.InvariantCulture));
                Append(hash, "\n");
                Append(hash, domain.RowsJson);
                Append(hash, "\n");
            }

            return VersionPrefix + Convert.ToHexStringLower(hash.GetHashAndReset());
        }

        public string ToShortVersion(string version)
        {
            if (version.StartsWith(VersionPrefix, StringComparison.Ordinal) && VersionPrefix.Length + 12 <= version.Length)
                return "cfg-" + version.Substring(VersionPrefix.Length, 12);

            return version;
        }

        private void Append(IncrementalHash hash, string value)
        {
            hash.AppendData(Encoding.UTF8.GetBytes(value));
        }
    }
}
