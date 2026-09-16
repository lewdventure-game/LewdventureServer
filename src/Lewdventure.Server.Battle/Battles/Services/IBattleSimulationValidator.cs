namespace Server.Battles
{
    internal interface IBattleSimulationValidator
    {
        public bool TryValidate(IBattleSimulationData data, out string errorMessage);
    }
}
