using Server.Configs;

namespace Server.Perks
{
    public interface IPerkGroupMapper : IConfigMapper
    {
        public int Id { get; }

        public PerkChoiceType ChoiceType { get; }

        public int[] PerkIds { get; }

        public int[] PerkChances { get; }

        public int RandomPerksCount { get; }

        public int ChoiceCount { get; }
    }
}
