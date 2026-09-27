using Server.Perks;

namespace Server.Battles
{
    internal interface IPerkCreator
    {
        public PerkType PerkType { get; }

        public string TypeKey { get; }

        public IPerk Create(IPerkMapper mapper);
    }
}
