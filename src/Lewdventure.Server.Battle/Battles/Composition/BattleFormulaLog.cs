namespace Server.Battles
{
    public sealed class BattleFormulaLog : IBattleFormulaRecorder
    {
        private readonly List<BattleFormula> _formulas = new();

        public bool IsEnabled => true;

        public IReadOnlyList<BattleFormula> Formulas => _formulas;

        public void Clear()
        {
            _formulas.Clear();
        }

        public void Record(BattleFormula formula)
        {
            _formulas.Add(formula);
        }
    }
}
