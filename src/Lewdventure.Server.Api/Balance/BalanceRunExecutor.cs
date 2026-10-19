using System.Globalization;
using Microsoft.Extensions.Options;
using Server.Api.Endpoints;
using Server.Api.Options;
using Server.Balance;
using Server.GameConfigs;
using Server.Infrastructure.Mongo.ConfigSnapshots;

namespace Server.Api.Balance
{
    internal sealed class BalanceRunExecutor
    {
        public const int MaxScenarioPayloadLength = 65536;

        private readonly BalanceConfigDrafts _balanceConfigDrafts;
        private readonly BalanceLevelSimulator _balanceLevelSimulator;
        private readonly BalanceOptions _balanceOptions;
        private readonly BalanceService _balanceService;
        private readonly BalanceSweepRunner _balanceSweepRunner;
        private readonly GameConfigSetCache _gameConfigSetCache;
        private readonly IGameConfigSetProvider _gameConfigSetProvider;

        public BalanceRunExecutor(
            BalanceConfigDrafts balanceConfigDrafts,
            BalanceLevelSimulator balanceLevelSimulator,
            IOptions<BalanceOptions> balanceOptions,
            BalanceService balanceService,
            BalanceSweepRunner balanceSweepRunner,
            GameConfigSetCache gameConfigSetCache,
            IGameConfigSetProvider gameConfigSetProvider)
        {
            _balanceConfigDrafts = balanceConfigDrafts;
            _balanceLevelSimulator = balanceLevelSimulator;
            _balanceOptions = balanceOptions.Value;
            _balanceService = balanceService;
            _balanceSweepRunner = balanceSweepRunner;
            _gameConfigSetCache = gameConfigSetCache;
            _gameConfigSetProvider = gameConfigSetProvider;
        }

        public string ValidateSeries(BalanceRequest request)
        {
            if (request.Battles < 1 || _balanceOptions.MaxSeriesBattles < request.Battles)
                return $"Battles must be from 1 to {_balanceOptions.MaxSeriesBattles}.";

            return ValidateScenario(request.Scenario);
        }

        public string ValidateLevel(BalanceLevelRequest request)
        {
            if (request.Runs < 1 || _balanceOptions.MaxLevelRuns < request.Runs)
                return $"Runs must be from 1 to {_balanceOptions.MaxLevelRuns}.";

            if (TryReadPolicy(request.PerkPolicy, request.PerkPriority, request.ForkPolicy, out _, out var policyError) == false)
                return policyError;

            return ValidateScenario(request.Scenario);
        }

        public string ValidateCompare(BalanceCompareRequest request)
        {
            if (IsKnownKind(request.Kind) == false)
                return UnknownKind(request.Kind);

            if (IsLevel(request.Kind) && (request.Runs < 1 || _balanceOptions.MaxLevelRuns < request.Runs))
                return $"Runs must be from 1 to {_balanceOptions.MaxLevelRuns}.";

            if (IsLevel(request.Kind) == false && (request.Battles < 1 || _balanceOptions.MaxSeriesBattles < request.Battles))
                return $"Battles must be from 1 to {_balanceOptions.MaxSeriesBattles}.";

            if (TryReadPolicy(request.PerkPolicy, request.PerkPriority, request.ForkPolicy, out _, out var policyError) == false)
                return policyError;

            return ValidateScenario(request.Scenario);
        }

        public string ValidateSweep(BalanceSweepRequest request)
        {
            if (IsKnownKind(request.Kind) == false)
                return UnknownKind(request.Kind);

            if (_balanceSweepRunner.TryCreateValues(request.Axis, _balanceOptions.MaxSweepPoints, out var values, out var axisError) == false)
                return axisError;

            if (IsLevel(request.Kind) && (request.Runs < 1 || _balanceOptions.MaxSweepRuns < request.Runs * values.Count))
                return $"Sweep runs ({values.Count} points x {request.Runs}) must be from 1 to {_balanceOptions.MaxSweepRuns}.";

            if (IsLevel(request.Kind) == false && (request.Battles < 1 || _balanceOptions.MaxSweepBattles < request.Battles * values.Count))
                return $"Sweep battles ({values.Count} points x {request.Battles}) must be from 1 to {_balanceOptions.MaxSweepBattles}.";

            if (TryReadPolicy(request.PerkPolicy, request.PerkPriority, request.ForkPolicy, out _, out var policyError) == false)
                return policyError;

            return ValidateScenario(request.Scenario);
        }

        public async Task<BalanceExecution> RunSeriesAsync(BalanceRequest request, IBalanceProgress progress, CancellationToken cancellationToken)
        {
            var resolution = await ResolveDraftAsync(request.Version, request.Edits, cancellationToken);

            if (resolution.Draft == null)
                return resolution.Failure!;

            var draft = resolution.Draft;

            if (_balanceService.TryRunSeries(request.Setup, request.Target, request.Seed, request.Battles, draft.Distributor!, progress, cancellationToken, out var series, out var error) == false)
                return Fail(error, StatusCodes.Status400BadRequest);

            return new BalanceExecution(series, string.Empty, StatusCodes.Status200OK, draft.Version, request.Edits.Count, $"серия x{series.Battles}: победы {series.Wins} ({Percent(series.WinRate)})", request.Scenario);
        }

        public async Task<BalanceExecution> RunLevelAsync(BalanceLevelRequest request, IBalanceProgress progress, CancellationToken cancellationToken)
        {
            TryReadPolicy(request.PerkPolicy, request.PerkPriority, request.ForkPolicy, out var policy, out _);

            var resolution = await ResolveDraftAsync(request.Version, request.Edits, cancellationToken);

            if (resolution.Draft == null)
                return resolution.Failure!;

            var draft = resolution.Draft;
            var levelRun = await _balanceLevelSimulator.RunAsync(request.Setup, request.StoryLevelId, request.Seed, request.Runs, policy, draft.Version, draft.Distributor!, progress, cancellationToken);

            if (levelRun.Result == null)
                return Fail(levelRun.Error, StatusCodes.Status400BadRequest);

            var level = levelRun.Result;
            var summary = $"уровень {level.StoryLevelId} x{level.Runs}: прошли {level.Completed} ({Percent(level.CompletionRate)}), этапов в среднем {level.StagesCleared.Average.ToString("0.##", CultureInfo.InvariantCulture)}";

            return new BalanceExecution(level, string.Empty, StatusCodes.Status200OK, draft.Version, request.Edits.Count, summary, request.Scenario);
        }

        public async Task<BalanceExecution> RunCompareAsync(BalanceCompareRequest request, IBalanceProgress progress, CancellationToken cancellationToken)
        {
            TryReadPolicy(request.PerkPolicy, request.PerkPriority, request.ForkPolicy, out var policy, out _);

            var resolutionA = await ResolveDraftAsync(request.A.Version, request.A.Edits, cancellationToken);

            if (resolutionA.Draft == null)
                return resolutionA.Failure!;

            var resolutionB = await ResolveDraftAsync(request.B.Version, request.B.Edits, cancellationToken);

            if (resolutionB.Draft == null)
                return resolutionB.Failure!;

            var draftA = resolutionA.Draft;
            var draftB = resolutionB.Draft;
            var isLevel = IsLevel(request.Kind);
            var response = new BalanceCompareResponse { Kind = isLevel ? BalanceSweepRunner.LevelKind : BalanceSweepRunner.SeriesKind, VersionA = draftA.Version, VersionB = draftB.Version };
            var edits = request.A.Edits.Count + request.B.Edits.Count;
            var version = draftA.Version + " | " + draftB.Version;

            if (isLevel)
            {
                var total = request.Runs * 2;
                var levelA = await _balanceLevelSimulator.RunAsync(request.A.Setup, request.StoryLevelId, request.Seed, request.Runs, policy, draftA.Version, draftA.Distributor!, new BalanceOffsetProgress(progress, 0, total), cancellationToken);

                if (levelA.Result == null)
                    return Fail("A: " + levelA.Error, StatusCodes.Status400BadRequest);

                var levelB = await _balanceLevelSimulator.RunAsync(request.B.Setup, request.StoryLevelId, request.Seed, request.Runs, policy, draftB.Version, draftB.Distributor!, new BalanceOffsetProgress(progress, request.Runs, total), cancellationToken);

                if (levelB.Result == null)
                    return Fail("B: " + levelB.Error, StatusCodes.Status400BadRequest);

                response.LevelA = levelA.Result;
                response.LevelB = levelB.Result;

                return new BalanceExecution(response, string.Empty, StatusCodes.Status200OK, version, edits, $"сравнение уровня {request.StoryLevelId} x{request.Runs}: A {Percent(levelA.Result.CompletionRate)} / B {Percent(levelB.Result.CompletionRate)}", request.Scenario);
            }

            var battlesTotal = request.Battles * 2;

            if (_balanceService.TryRunSeries(request.A.Setup, request.Target, (ulong)request.Seed, request.Battles, draftA.Distributor!, new BalanceOffsetProgress(progress, 0, battlesTotal), cancellationToken, out var seriesA, out var errorA) == false)
                return Fail("A: " + errorA, StatusCodes.Status400BadRequest);

            if (_balanceService.TryRunSeries(request.B.Setup, request.Target, (ulong)request.Seed, request.Battles, draftB.Distributor!, new BalanceOffsetProgress(progress, request.Battles, battlesTotal), cancellationToken, out var seriesB, out var errorB) == false)
                return Fail("B: " + errorB, StatusCodes.Status400BadRequest);

            response.SeriesA = seriesA;
            response.SeriesB = seriesB;

            return new BalanceExecution(response, string.Empty, StatusCodes.Status200OK, version, edits, $"сравнение серий x{request.Battles}: A {Percent(seriesA.WinRate)} / B {Percent(seriesB.WinRate)}", request.Scenario);
        }

        public async Task<BalanceExecution> RunSweepAsync(BalanceSweepRequest request, IBalanceProgress progress, CancellationToken cancellationToken)
        {
            TryReadPolicy(request.PerkPolicy, request.PerkPriority, request.ForkPolicy, out var policy, out _);
            _balanceSweepRunner.TryCreateValues(request.Axis, _balanceOptions.MaxSweepPoints, out var values, out _);

            var configSet = await ResolveAsync(request.Version, cancellationToken);

            if (configSet == null)
                return Fail($"Config version {request.Version} is not published.", StatusCodes.Status404NotFound);

            var result = IsLevel(request.Kind)
                ? await _balanceSweepRunner.RunLevelAsync(configSet, request.Edits, request.Axis, values, request.Setup, request.StoryLevelId, request.Seed, request.Runs, policy, progress, cancellationToken)
                : _balanceSweepRunner.RunSeries(configSet, request.Edits, request.Axis, values, request.Setup, request.Target, (ulong)request.Seed, request.Battles, progress, cancellationToken);
            var axis = result.Axis;
            var summary = $"перебор {result.Kind} {axis.Sheet} / {axis.Row} / {axis.Column} от {axis.From.ToString(CultureInfo.InvariantCulture)} до {axis.To.ToString(CultureInfo.InvariantCulture)}: {result.Points.Count} точек";

            return new BalanceExecution(result, string.Empty, StatusCodes.Status200OK, configSet.Version, request.Edits.Count, summary, request.Scenario);
        }

        public async Task<BalanceDraftResolution> ResolveDraftAsync(string? version, List<BalanceConfigEdit>? edits, CancellationToken cancellationToken)
        {
            var configSet = await ResolveAsync(version, cancellationToken);

            if (configSet == null)
                return new BalanceDraftResolution(null, null, Fail($"Config version {version} is not published.", StatusCodes.Status404NotFound));

            var draft = _balanceConfigDrafts.Apply(configSet, edits ?? new List<BalanceConfigEdit>());

            if (draft.Distributor == null)
                return new BalanceDraftResolution(configSet, null, Fail("Draft edits are invalid: " + string.Join("; ", draft.Errors), StatusCodes.Status400BadRequest));

            return new BalanceDraftResolution(configSet, draft, null);
        }

        public async Task<GameConfigSet?> ResolveAsync(string? version, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(version))
                return _gameConfigSetProvider.Current;

            return await _gameConfigSetCache.GetAsync(version.Trim(), cancellationToken);
        }

        public bool TryReadPolicy(string perkPolicyText, List<int> perkPriority, string forkPolicyText, out BalanceLevelPolicy policy, out string error)
        {
            policy = new BalanceLevelPolicy { PerkPriority = new List<int>(perkPriority ?? new List<int>()) };
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(perkPolicyText) == false)
            {
                if (Enum.TryParse<BalancePerkPolicy>(perkPolicyText, true, out var perkPolicy) == false)
                {
                    error = $"Unknown perk policy {perkPolicyText}, use first, random or priority.";

                    return false;
                }

                policy.PerkPolicy = perkPolicy;
            }

            if (string.IsNullOrWhiteSpace(forkPolicyText) == false)
            {
                if (Enum.TryParse<BalanceForkPolicy>(forkPolicyText, true, out var forkPolicy) == false)
                {
                    error = $"Unknown fork policy {forkPolicyText}, use firstButton, secondButton or random.";

                    return false;
                }

                policy.ForkPolicy = forkPolicy;
            }

            return true;
        }

        private string ValidateScenario(string scenario)
        {
            if (MaxScenarioPayloadLength < (scenario ?? string.Empty).Length)
                return $"Scenario must be at most {MaxScenarioPayloadLength} characters.";

            return string.Empty;
        }

        private bool IsKnownKind(string kind)
        {
            return IsLevel(kind) || string.Equals(kind, BalanceSweepRunner.SeriesKind, StringComparison.OrdinalIgnoreCase);
        }

        private bool IsLevel(string kind)
        {
            return string.Equals(kind, BalanceSweepRunner.LevelKind, StringComparison.OrdinalIgnoreCase);
        }

        private string UnknownKind(string kind)
        {
            return $"Unknown kind {kind}, use series or level.";
        }

        private BalanceExecution Fail(string error, int statusCode)
        {
            return new BalanceExecution(null, error, statusCode, string.Empty, 0, string.Empty, string.Empty);
        }

        private string Percent(float value)
        {
            return (value * 100f).ToString("0.#", CultureInfo.InvariantCulture) + " %";
        }
    }
}
