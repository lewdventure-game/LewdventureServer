using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.Serialization;

namespace Server.Common
{
    public sealed class RarityReader
    {
        private readonly Dictionary<string, RarityType> _byName = new(StringComparer.OrdinalIgnoreCase);

        public RarityReader()
        {
            var fields = typeof(RarityType).GetFields(BindingFlags.Public | BindingFlags.Static);

            for (int i = 0; i < fields.Length; i++)
            {
                var field = fields[i];
                var value = (RarityType)field.GetValue(null)!;
                var attribute = field.GetCustomAttribute<EnumMemberAttribute>();
                var name = attribute == null || string.IsNullOrEmpty(attribute.Value) ? field.Name.ToLowerInvariant() : attribute.Value!;

                _byName[name] = value;
            }
        }

        public bool TryRead(string rawValue, [MaybeNullWhen(false)] out RarityType rarity)
        {
            rarity = RarityType.Unknown;

            if (string.IsNullOrWhiteSpace(rawValue))
                return false;

            if (_byName.TryGetValue(rawValue.Trim(), out rarity) == false)
                return false;

            return rarity != RarityType.Unknown;
        }
    }
}
