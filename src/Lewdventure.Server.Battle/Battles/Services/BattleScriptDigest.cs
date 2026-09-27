using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace Server.Battles
{
    internal sealed class BattleScriptDigest : IBattleScriptDigest
    {
        private const string DigestPrefix = "sha256:";

        private readonly JsonSerializerSettings _serializerSettings = new BattleJsonSettingsFactory().Create();

        public string Compute(IBattleScriptResponse script)
        {
            var json = JsonConvert.SerializeObject(script, _serializerSettings);

            using var algorithm = SHA256.Create();

            var hash = algorithm.ComputeHash(Encoding.UTF8.GetBytes(json));
            var builder = new StringBuilder(DigestPrefix.Length + hash.Length * 2);

            builder.Append(DigestPrefix);

            for (int i = 0; i < hash.Length; i++)
                builder.Append(hash[i].ToString("x2", System.Globalization.CultureInfo.InvariantCulture));

            return builder.ToString();
        }
    }
}
