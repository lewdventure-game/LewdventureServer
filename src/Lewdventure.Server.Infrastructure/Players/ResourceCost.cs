namespace Server.Infrastructure.Players
{
    internal readonly struct ResourceCost
    {
        private readonly string _key;
        private readonly int _amount;

        public ResourceCost(string key, int amount)
        {
            _key = key;
            _amount = amount;
        }

        public string Key => _key;

        public int Amount => _amount;
    }
}
