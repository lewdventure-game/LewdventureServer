using System.Diagnostics.CodeAnalysis;
using Server.Statuses;

namespace Server.Battles
{
    internal interface IStatusParametersParser
    {
        public bool TryParse(string parameters, StatusType statusType, [MaybeNullWhen(false)] out StatusParameters parsed);
    }
}
