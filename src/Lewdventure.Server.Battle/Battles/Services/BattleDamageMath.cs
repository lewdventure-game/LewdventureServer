namespace Server.Battles
{
    internal sealed class BattleDamageMath : IBattleDamageMath
    {
        private readonly IBattleFormulaRecorder _battleFormulaRecorder;

        public BattleDamageMath(IBattleFormulaRecorder battleFormulaRecorder)
        {
            _battleFormulaRecorder = battleFormulaRecorder;
        }

        public bool IsRecordingFormulas => _battleFormulaRecorder.IsEnabled;

        public float RoundDamage(float damage)
        {
            if (damage <= 0f)
                return 0f;

            return MathF.Round(damage, MidpointRounding.AwayFromZero);
        }

        public void RecordFormula(BattleFormula formula)
        {
            _battleFormulaRecorder.Record(formula);
        }
    }
}
