namespace Server.Battles
{
    internal sealed class DisabledBattleFormulaRecorder : IBattleFormulaRecorder
    {
        public bool IsEnabled => false;

        public void Record(BattleFormula formula)
        {
        }
    }
}
