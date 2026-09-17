namespace Server.GameConfigs
{
    internal interface IGameConfigSetProvider
    {
        public GameConfigSet Current { get; }

        public void Swap(GameConfigSet configSet);
    }
}
