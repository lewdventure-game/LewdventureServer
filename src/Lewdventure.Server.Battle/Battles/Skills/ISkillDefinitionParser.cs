namespace Server.Battles
{
    internal interface ISkillDefinitionParser
    {
        public bool TryParse(Server.Skills.ISkillMapper mapper, out SkillDefinition definition);
    }
}
