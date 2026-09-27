namespace Server.GameConfigs
{
    internal sealed class EffectParameterDescriptor
    {
        public EffectParameterDescriptor(string effectType, string[] requiredKeys, string[] optionalKeys)
        {
            EffectType = effectType;
            RequiredKeys = requiredKeys;
            OptionalKeys = optionalKeys;
        }

        public string EffectType { get; }

        public string[] RequiredKeys { get; }

        public string[] OptionalKeys { get; }
    }
}
