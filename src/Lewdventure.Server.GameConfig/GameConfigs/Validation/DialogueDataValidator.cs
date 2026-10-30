using Server.Dialogues;
using Server.Services;

namespace Server.GameConfigs
{
    internal sealed class DialogueDataValidator
    {
        private const string DialoguesSheet = "Лист Dialogues";
        private const string StoryLevelsSheet = "Лист Story_levels";
        private const int FirstStep = 1;
        private const float MinScreenPercent = 0f;
        private const float MaxScreenPercent = 100f;

        private readonly List<IDialogueStepMapper> _steps = new();
        private readonly HashSet<int> _dialogueIds = new();
        private readonly HashSet<int> _stepNumbers = new();
        private readonly HashSet<int> _reachesEnd = new();

        public void Collect(IConfigDistributor distributor, List<string> errors, List<string> warnings)
        {
            CollectDialogues(distributor.Dialogues, errors, warnings);
            CollectStoryLevels(distributor, errors);
        }

        private void CollectDialogues(IDialogueStepMapperManager dialogues, List<string> errors, List<string> warnings)
        {
            _dialogueIds.Clear();

            var collection = dialogues.Collection;

            for (int i = 0; i < collection.Count; i++)
                _dialogueIds.Add(collection[i].Id);

            foreach (var dialogueId in _dialogueIds)
            {
                dialogues.CopySteps(dialogueId, _steps);

                CollectStepNumbers(dialogueId, errors);
                CollectSteps(dialogueId, errors, warnings);
                CollectReachability(dialogueId, errors);
            }
        }

        private void CollectStepNumbers(int dialogueId, List<string> errors)
        {
            _stepNumbers.Clear();

            for (int i = 0; i < _steps.Count; i++)
            {
                var step = _steps[i].Step;

                if (_stepNumbers.Add(step) == false)
                    errors.Add($"{DialoguesSheet}, диалог {dialogueId}: шаг {step} записан дважды.");
            }

            if (_stepNumbers.Contains(FirstStep) == false)
                errors.Add($"{DialoguesSheet}, диалог {dialogueId}: нет шага {FirstStep}, диалог не с чего начать.");
        }

        private void CollectSteps(int dialogueId, List<string> errors, List<string> warnings)
        {
            for (int i = 0; i < _steps.Count; i++)
            {
                var step = _steps[i];
                var place = $"{DialoguesSheet}, диалог {dialogueId} шаг {step.Step}";
                var choiceCount = step.LocalizationKeys.Length - 1;

                if (step.LocalizationKeys.Length == 0)
                    errors.Add($"{place}: пустой localization_keys, нет текста шага.");

                if (0 < choiceCount && step.NextSteps.Length != choiceCount)
                    errors.Add($"{place}: вариантов выбора {choiceCount}, а переходов в next_step {step.NextSteps.Length}. Нужно по одному переходу на вариант.");

                if (choiceCount <= 0 && 1 < step.NextSteps.Length)
                    errors.Add($"{place}: переходов в next_step {step.NextSteps.Length} без вариантов выбора. Без выбора переход один.");

                for (int nextIndex = 0; nextIndex < step.NextSteps.Length; nextIndex++)
                {
                    var nextStep = step.NextSteps[nextIndex];

                    if (_stepNumbers.Contains(nextStep) == false)
                        errors.Add($"{place}: next_step ведёт на шаг {nextStep}, которого нет в диалоге.");
                }

                if (step.TextSpeed <= 0f)
                    errors.Add($"{place}: text_speed {step.TextSpeed}, нужно больше нуля (символов в секунду).");

                for (int characterIndex = 0; characterIndex < step.Characters.Length; characterIndex++)
                {
                    var character = step.Characters[characterIndex];

                    if (character.ScreenPercent < MinScreenPercent || MaxScreenPercent < character.ScreenPercent)
                        warnings.Add($"{place}: спрайт {character.SpriteName} на {character.ScreenPercent}% ширины, за пределами 0–100.");
                }

                if (step.Sounds.Length != 0 && step.Backgrounds.Length != 0 && step.Sounds.Length != step.Backgrounds.Length)
                    warnings.Add($"{place}: фонов {step.Backgrounds.Length}, а оркестров в sounds {step.Sounds.Length}. Оркестры идут по фонам в том же порядке, пустое место — без звука.");
            }
        }

        private void CollectReachability(int dialogueId, List<string> errors)
        {
            _reachesEnd.Clear();

            var changed = true;

            while (changed)
            {
                changed = false;

                for (int i = 0; i < _steps.Count; i++)
                {
                    var step = _steps[i];

                    if (_reachesEnd.Contains(step.Step) || LeadsToEnd(step) == false)
                        continue;

                    _reachesEnd.Add(step.Step);
                    changed = true;
                }
            }

            for (int i = 0; i < _steps.Count; i++)
            {
                var step = _steps[i];

                if (_reachesEnd.Contains(step.Step) == false)
                    errors.Add($"{DialoguesSheet}, диалог {dialogueId} шаг {step.Step}: из шага нельзя дойти до конца диалога (шага с пустым next_step), игрок застрянет в цикле.");
            }
        }

        private bool LeadsToEnd(IDialogueStepMapper step)
        {
            if (step.NextSteps.Length == 0)
                return true;

            for (int i = 0; i < step.NextSteps.Length; i++)
            {
                if (_reachesEnd.Contains(step.NextSteps[i]))
                    return true;
            }

            return false;
        }

        private void CollectStoryLevels(IConfigDistributor distributor, List<string> errors)
        {
            var levels = distributor.StoryLevels.Collection;

            for (int i = 0; i < levels.Count; i++)
            {
                var level = levels[i];
                var place = $"{StoryLevelsSheet}, уровень {level.Id}";

                if (level.DialogueIds.Length != level.DialogueTriggers.Length)
                {
                    errors.Add($"{place}: диалогов в dialogues_ids {level.DialogueIds.Length}, а триггеров в dialogues_triggers {level.DialogueTriggers.Length}. Нужно по одному триггеру на диалог.");

                    continue;
                }

                for (int dialogueIndex = 0; dialogueIndex < level.DialogueIds.Length; dialogueIndex++)
                {
                    var dialogueId = level.DialogueIds[dialogueIndex];
                    var trigger = level.DialogueTriggers[dialogueIndex];

                    if (distributor.Dialogues.Contains(dialogueId) == false)
                        errors.Add($"{place}: диалога {dialogueId} нет в листе Dialogues.");

                    if (trigger.Type == DialogueTriggerType.Unknown)
                        errors.Add($"{place}: триггер «{trigger.Source}» не распознан. Допустимо on_start, on_win, on_loss, on_stage_entered:N.");

                    if (trigger.Type == DialogueTriggerType.OnStageEntered && (trigger.StageNumber < 1 || level.StageIds.Length < trigger.StageNumber))
                        errors.Add($"{place}: «{trigger.Source}» — этапа {trigger.StageNumber} нет, в уровне этапов {level.StageIds.Length} (счёт с 1).");
                }
            }
        }
    }
}
