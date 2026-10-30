using Server.Services;

namespace Server.Infrastructure.Players
{
    internal sealed class DialoguePathValidator
    {
        private const int FirstStep = 1;
        private const int MaxVisitedSteps = 1000;

        public string Validate(int dialogueId, IReadOnlyList<int> picks, IConfigDistributor configDistributor)
        {
            var stepNumber = FirstStep;
            var pickIndex = 0;

            for (int visited = 0; visited < MaxVisitedSteps; visited++)
            {
                if (configDistributor.Dialogues.TryGet(dialogueId, stepNumber, out var step) == false)
                    return $"Dialogue {dialogueId} has no step {stepNumber}.";

                if (step!.NextSteps.Length == 0)
                    return pickIndex == picks.Count ? string.Empty : $"Dialogue {dialogueId} ended before pick {pickIndex + 1}.";

                if (step.LocalizationKeys.Length <= 1)
                {
                    stepNumber = step.NextSteps[0];

                    continue;
                }

                if (picks.Count <= pickIndex)
                    return $"Dialogue {dialogueId} step {stepNumber} needs a pick.";

                var pick = picks[pickIndex];

                if (pick < 1 || step.NextSteps.Length < pick)
                    return $"Dialogue {dialogueId} step {stepNumber} has no choice {pick}.";

                pickIndex += 1;
                stepNumber = step.NextSteps[pick - 1];
            }

            return $"Dialogue {dialogueId} path is longer than {MaxVisitedSteps} steps.";
        }
    }
}
