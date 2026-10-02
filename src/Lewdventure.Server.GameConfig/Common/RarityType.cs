using System.Runtime.Serialization;

namespace Server.Common
{
    public enum RarityType
    {
        Unknown = 0,
        [EnumMember(Value = "common")]
        Common = 1,
        [EnumMember(Value = "rare")]
        Rare = 2,
        [EnumMember(Value = "epic")]
        Epic = 3,
        [EnumMember(Value = "legendary")]
        Legendary = 4,
        [EnumMember(Value = "mythic")]
        Mythic = 5,
        [EnumMember(Value = "uncommon")]
        Uncommon = 6,
        [EnumMember(Value = "epic_1")]
        Epic1 = 7,
        [EnumMember(Value = "epic_2")]
        Epic2 = 8,
        [EnumMember(Value = "legendary_1")]
        Legendary1 = 9,
        [EnumMember(Value = "legendary_2")]
        Legendary2 = 10,
        [EnumMember(Value = "legendary_3")]
        Legendary3 = 11,
        [EnumMember(Value = "mythic_1")]
        Mythic1 = 12,
        [EnumMember(Value = "mythic_2")]
        Mythic2 = 13,
        [EnumMember(Value = "mythic_3")]
        Mythic3 = 14,
        [EnumMember(Value = "mythic_4")]
        Mythic4 = 15,
    }
}
