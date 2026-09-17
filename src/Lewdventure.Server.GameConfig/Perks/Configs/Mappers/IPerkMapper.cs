using Server.Common;
using Server.Configs;

namespace Server.Perks
{
    internal interface IPerkMapper : IConfigMapper
    {
        public int Id { get; }

        public string IconArt { get; }

        public RarityType Rarity { get; }

        public PerkType PerkType { get; }

        public string PerkParameters { get; }

        public bool IsMultiplePicks { get; }

        public int TriggerOrder { get; }

        public string NameLocalizationKey { get; }

        public string DescriptionLocalizationKey { get; }
    }
}
