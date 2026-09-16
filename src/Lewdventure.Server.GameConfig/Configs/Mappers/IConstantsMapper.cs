namespace Server.Configs
{
    internal interface IConstantsMapper : IConfigMapper
    {
        public string ConstantName { get; }

        public string ConstantValue { get; }

        public ValueType ConstantType { get; }
    }
}
