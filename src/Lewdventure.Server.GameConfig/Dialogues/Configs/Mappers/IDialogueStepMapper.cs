using Server.Configs;

namespace Server.Dialogues
{
    public interface IDialogueStepMapper : IConfigMapper
    {
        public int Id { get; }

        public int Step { get; }

        public int[] NextSteps { get; }

        public bool IsFadeOnStart { get; }

        public DialogueCharacter[] Characters { get; }

        public string CurrentCharacterLocName { get; }

        public string[] Backgrounds { get; }

        public string Music { get; }

        public string[] Sounds { get; }

        public string[] LocalizationKeys { get; }

        public float TextSpeed { get; }
    }
}
