namespace Server.Battles
{
    internal interface IStatusParametersParser
    {
        public StatusParameters Parse(string parameters);
    }
}
