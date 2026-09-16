namespace Server.Battles
{
    internal interface IBattleParameterParser
    {
        public void ParseKeyValues(string parameters, Dictionary<string, string> output);

        public string UnwrapBrackets(string value);
    }
}
