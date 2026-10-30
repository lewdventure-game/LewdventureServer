namespace Server.Dialogues
{
    public sealed class DialogueCharacter
    {
        private readonly string _spriteName;
        private readonly float _screenPercent;

        public string SpriteName => _spriteName;

        public float ScreenPercent => _screenPercent;

        public DialogueCharacter(string spriteName, float screenPercent)
        {
            _spriteName = spriteName;
            _screenPercent = screenPercent;
        }
    }
}
