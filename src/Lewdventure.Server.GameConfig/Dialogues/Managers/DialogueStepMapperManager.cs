using System.Diagnostics.CodeAnalysis;
using Server.Collections;

namespace Server.Dialogues
{
    internal sealed class DialogueStepMapperManager : BaseManager<IDialogueStepMapper>, IDialogueStepMapperManager
    {
        public bool Contains(int dialogueId)
        {
            for (int i = 0; i < Collection.Count; i++)
            {
                if (Collection[i].Id == dialogueId)
                    return true;
            }

            return false;
        }

        public bool TryGet(int dialogueId, int step, [MaybeNullWhen(false)] out IDialogueStepMapper mapper)
        {
            for (int i = 0; i < Collection.Count; i++)
            {
                var currentMapper = Collection[i];

                if (currentMapper.Id != dialogueId || currentMapper.Step != step)
                    continue;

                mapper = currentMapper;

                return true;
            }

            mapper = null;

            return false;
        }

        public void CopySteps(int dialogueId, List<IDialogueStepMapper> steps)
        {
            steps.Clear();

            for (int i = 0; i < Collection.Count; i++)
            {
                var currentMapper = Collection[i];

                if (currentMapper.Id == dialogueId)
                    steps.Add(currentMapper);
            }
        }
    }
}
