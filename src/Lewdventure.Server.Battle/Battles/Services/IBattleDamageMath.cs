namespace Server.Battles
{
    internal interface IBattleDamageMath
    {
        public bool IsRecordingFormulas { get; }

        public float RoundDamage(float damage);

        public void RecordFormula(BattleFormula formula);
    }
}
