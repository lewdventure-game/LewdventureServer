namespace Server.Configs
{
    public interface IConstantsMapper : IConfigMapper
    {
        public string ConstantName { get; }

        public string ConstantValue { get; }

        public ValueType ConstantType { get; }
    }
}
