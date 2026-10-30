using System.Diagnostics.CodeAnalysis;
using Server.Collections;

namespace Server.Dialogues
{
    public interface IDialogueStepMapperManager : IManager<IDialogueStepMapper>
    {
        public bool Contains(int dialogueId);

        public bool TryGet(int dialogueId, int step, [MaybeNullWhen(false)] out IDialogueStepMapper mapper);

        public void CopySteps(int dialogueId, List<IDialogueStepMapper> steps);
    }
}
