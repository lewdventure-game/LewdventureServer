namespace Server.Configs
{
    [AttributeUsage(AttributeTargets.Property)]
    internal sealed class OptionalColumnAttribute : Attribute
    {
        public OptionalColumnAttribute(string reason)
        {
            Reason = reason;
        }

        public string Reason { get; }
    }
}
