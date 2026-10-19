using System.Globalization;
using Server.GameConfigs;

namespace Server.Balance
{
    internal sealed class BalanceSweepRunner
    {
        public const string SeriesKind = "series";
        public const string LevelKind = "level";

        private const double Epsilon = 1e-9;

        private readonly BalanceConfigDrafts _balanceConfigDrafts;
        private readonly BalanceLevelSimulator _balanceLevelSimulator;
        private readonly BalanceService _balanceService;

        public BalanceSweepRunner(BalanceConfigDrafts balanceConfigDrafts, BalanceLevelSimulator balanceLevelSimulator, BalanceService balanceService)
        {
            _balanceConfigDrafts = balanceConfigDrafts;
            _balanceLevelSimulator = balanceLevelSimulator;
            _balanceService = balanceService;
        }

        public bool TryCreateValues(BalanceSweepAxis axis, int maxPoints, out List<double> values, out string error)
        {
            values = new List<double>();
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(axis.Sheet) || string.IsNullOrWhiteSpace(axis.Row) || string.IsNullOrWhiteSpace(axis.Column))
            {
                error = "Sweep needs sheet, row and column.";

                return false;
            }

            if (axis.Step <= 0 || double.IsFinite(axis.Step) == false || double.IsFinite(axis.From) == false || double.IsFinite(axis.To) == false)
            {
                error = "Sweep step must be a positive number.";

                return false;
            }

            if (axis.To < axis.From)
            {
                error = "Sweep end must not be less than its start.";

                return false;
            }

            var count = (int)Math.Floor((axis.To - axis.From) / axis.Step + Epsilon) + 1;

            if (maxPoints < count)
            {
                error = $"Sweep has {count} points, the limit is {maxPoints}.";

                return false;
            }

            for (int i = 0; i < count; i++)
                values.Add(Math.Round(axis.From + axis.Step * i, 6));

            return true;
        }

        public BalanceSweepResult RunSeries(
            GameConfigSet configSet,
            List<BalanceConfigEdit> edits,
            BalanceSweepAxis axis,
            List<double> values,
            BalanceSetup setup,
            BalanceTarget target,
            ulong firstSeed,
            int battles,
            IBalanceProgress progress,
            CancellationToken cancellationToken)
        {
            var result = new BalanceSweepResult { Kind = SeriesKind, Axis = axis };
            var total = battles * values.Count;

            for (int i = 0; i < values.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var point = CreatePoint(values[i]);
                var draft = _balanceConfigDrafts.Apply(configSet, WithAxis(edits, axis, point.Value));

                result.Points.Add(point);

                if (draft.Distributor == null)
                {
                    point.Error = string.Join("; ", draft.Errors);
                    progress.Report(battles * (i + 1), total);

                    continue;
                }

                if (_balanceService.TryRunSeries(setup, target, firstSeed, battles, draft.Distributor, new BalanceOffsetProgress(progress, battles * i, total), cancellationToken, out var series, out var error) == false)
                {
                    point.Error = error;

                    continue;
                }

                point.WinRate = series.WinRate;
                point.TurnsAverage = series.Turns.Average;
                point.HeroHealthOnWin = series.HeroHealthEndOnWin.Average;
            }

            return result;
        }

        public async Task<BalanceSweepResult> RunLevelAsync(
            GameConfigSet configSet,
            List<BalanceConfigEdit> edits,
            BalanceSweepAxis axis,
            List<double> values,
            BalanceSetup setup,
            int storyLevelId,
            long firstSeed,
            int runs,
            BalanceLevelPolicy policy,
            IBalanceProgress progress,
            CancellationToken cancellationToken)
        {
            var result = new BalanceSweepResult { Kind = LevelKind, Axis = axis };
            var total = runs * values.Count;

            for (int i = 0; i < values.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var point = CreatePoint(values[i]);
                var draft = _balanceConfigDrafts.Apply(configSet, WithAxis(edits, axis, point.Value));

                result.Points.Add(point);

                if (draft.Distributor == null)
                {
                    point.Error = string.Join("; ", draft.Errors);
                    progress.Report(runs * (i + 1), total);

                    continue;
                }

                var levelRun = await _balanceLevelSimulator.RunAsync(setup, storyLevelId, firstSeed, runs, policy, draft.Version, draft.Distributor, new BalanceOffsetProgress(progress, runs * i, total), cancellationToken);

                if (levelRun.Result == null)
                {
                    point.Error = levelRun.Error;

                    continue;
                }

                point.CompletionRate = levelRun.Result.CompletionRate;
                point.StagesClearedAverage = levelRun.Result.StagesCleared.Average;
                point.ExperienceLevelAverage = levelRun.Result.FinalExperienceLevel.Average;
            }

            return result;
        }

        private BalanceSweepPoint CreatePoint(double value)
        {
            return new BalanceSweepPoint { Number = value, Value = value.ToString("0.######", CultureInfo.InvariantCulture) };
        }

        private List<BalanceConfigEdit> WithAxis(List<BalanceConfigEdit> edits, BalanceSweepAxis axis, string value)
        {
            var result = new List<BalanceConfigEdit>(edits.Count + 1);

            result.AddRange(edits);
            result.Add(new BalanceConfigEdit { Sheet = axis.Sheet, Row = axis.Row, Column = axis.Column, Value = value });

            return result;
        }
    }
}
