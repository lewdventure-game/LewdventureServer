namespace Server.Dialogues
{
    public sealed class DialogueTrigger
    {
        private readonly DialogueTriggerType _type;
        private readonly int _stageNumber;
        private readonly string _source;

        public DialogueTriggerType Type => _type;

        public int StageNumber => _stageNumber;

        public string Source => _source;

        public DialogueTrigger(DialogueTriggerType type, int stageNumber, string source)
        {
            _type = type;
            _stageNumber = stageNumber;
            _source = source;
        }
    }
}
