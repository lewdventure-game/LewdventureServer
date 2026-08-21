using Server.Perks;

namespace Server.Battles
{
    internal interface IPerkFactory
    {
        public IPerk Create(IPerkMapper mapper);
    }
}
