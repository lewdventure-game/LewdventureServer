using Server.GameConfigs;

namespace Server.Infrastructure.GameConfigs
{
    internal sealed class GameConfigSelection
    {
        private readonly IGameConfigSetProvider _gameConfigSetProvider;

        private GameConfigSet? _current;

        public GameConfigSelection(IGameConfigSetProvider gameConfigSetProvider)
        {
            _gameConfigSetProvider = gameConfigSetProvider;
        }

        public GameConfigSet Current
        {
            get
            {
                if (_current == null)
                    _current = _gameConfigSetProvider.Current;

                return _current;
            }
        }

        public void Select(GameConfigSet configSet)
        {
            _current = configSet;
        }
    }
}
