using System.Globalization;
using Microsoft.Extensions.Options;
using Server.Admin.Options;

namespace Server.Admin.Analytics
{
    internal sealed class AnalyticsReportService
    {
        public const int MaxSeries = 8;

        private const string WinOutcome = "TeamAWin";
        private const string CompletedStatus = "completed";

        private readonly AnalyticsQueryBuilder _analyticsQueryBuilder;
        private readonly ClickHouseQueryClient _clickHouseQueryClient;
        private readonly AdminPanelOptions _options;
        private readonly TimeProvider _timeProvider;

        public AnalyticsReportService(
            AnalyticsQueryBuilder analyticsQueryBuilder,
            ClickHouseQueryClient clickHouseQueryClient,
            IOptions<AdminPanelOptions> options,
            TimeProvider timeProvider)
        {
            _analyticsQueryBuilder = analyticsQueryBuilder;
            _clickHouseQueryClient = clickHouseQueryClient;
            _options = options.Value;
            _timeProvider = timeProvider;
        }

        public async Task<AnalyticsReport> BuildEventReportAsync(string environment, AnalyticsFilter filter, CancellationToken cancellationToken)
        {
            var report = new AnalyticsReport();
            var query = Prepare(environment, filter, report.Errors);

            if (query == null)
                return report;

            var source = $"FROM {query.Table} WHERE {query.Where}";
            var totals = await RunAsync($"SELECT count() AS events, uniqExact(user_id) AS users, {query.MetricExpression} AS metric {source}", query.Parameters, report.Errors, cancellationToken);

            if (totals != null && 0 < totals.Rows.Count)
            {
                report.Events = (long)totals.ReadDouble(0, "events");
                report.Users = (long)totals.ReadDouble(0, "users");
                report.MetricValue = totals.ReadDouble(0, "metric");
            }

            var topGroups = $"SELECT {query.GroupExpression} {source} GROUP BY {query.GroupExpression} ORDER BY {query.MetricExpression} DESC LIMIT {MaxSeries}";
            var series = await RunAsync($"SELECT {query.BucketExpression} AS t, {query.GroupExpression} AS g, {query.MetricExpression} AS v {source} AND {query.GroupExpression} IN ({topGroups}) GROUP BY t, g ORDER BY t", query.Parameters, report.Errors, cancellationToken);

            ReadSeries(series, report.Series);

            var breakdown = await RunAsync($"SELECT {query.GroupExpression} AS g, count() AS events, uniqExact(user_id) AS users, {query.MetricExpression} AS metric {source} GROUP BY g ORDER BY metric DESC LIMIT 50", query.Parameters, report.Errors, cancellationToken);

            ReadBreakdown(breakdown, report.Breakdown);

            var latest = await RunAsync($"SELECT event_time, event_type, user_id, country, experiment_id, group_id, source, app_version, event_properties {source} ORDER BY event_time DESC LIMIT 100", query.Parameters, report.Errors, cancellationToken);

            ReadEvents(latest, report.Latest);

            var period = $"FROM {query.Table} WHERE event_time >= {{from:DateTime64(3)}} AND event_time < {{to:DateTime64(3)}}";
            var eventTypes = await RunAsync($"SELECT event_type AS g, count() AS c {period} GROUP BY g ORDER BY c DESC LIMIT 200", query.Parameters, report.Errors, cancellationToken);

            ReadStrings(eventTypes, "g", report.EventTypes);

            if (filter.EventType.Length != 0)
            {
                var keys = await RunAsync($"SELECT DISTINCT arrayJoin(JSONExtractKeys(event_properties)) AS g {period} AND event_type = {{event_type:String}} ORDER BY g LIMIT 100", query.Parameters, report.Errors, cancellationToken);

                ReadStrings(keys, "g", report.PropertyKeys);
            }

            return report;
        }

        public async Task<OverviewReport> BuildOverviewAsync(string environment, AnalyticsFilter filter, CancellationToken cancellationToken)
        {
            var report = new OverviewReport();
            var query = Prepare(environment, new AnalyticsFilter { From = filter.From, To = filter.To }, report.Errors);

            if (query == null)
                return report;

            filter.From = query.From.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            filter.To = query.To.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            var source = $"FROM {query.Table} WHERE {query.Where}";
            var totals = await RunAsync(
                $"SELECT uniqExact(user_id) AS players, countIf(event_type = 'account_created') AS new_players, countIf(event_type = 'session_start') AS sessions, countIf(event_type = 'run_started') AS runs_started, countIf(event_type = 'run_finished' AND JSONExtractString(event_properties, 'status') = '{CompletedStatus}') AS runs_completed, round(100 * countIf(event_type = 'battle_finished' AND JSONExtractString(event_properties, 'outcome') = '{WinOutcome}') / greatest(countIf(event_type = 'battle_finished'), 1), 1) AS win_rate {source}",
                query.Parameters,
                report.Errors,
                cancellationToken);

            if (totals != null && 0 < totals.Rows.Count)
            {
                report.ActivePlayers = (long)totals.ReadDouble(0, "players");
                report.NewPlayers = (long)totals.ReadDouble(0, "new_players");
                report.Sessions = (long)totals.ReadDouble(0, "sessions");
                report.RunsStarted = (long)totals.ReadDouble(0, "runs_started");
                report.RunsCompleted = (long)totals.ReadDouble(0, "runs_completed");
                report.BattleWinRate = totals.ReadDouble(0, "win_rate");
            }

            ReadSeries(await RunAsync($"SELECT toStartOfDay(event_time) AS t, 'игроки' AS g, uniqExact(user_id) AS v {source} GROUP BY t ORDER BY t", query.Parameters, report.Errors, cancellationToken), report.ActivePlayersSeries);
            ReadSeries(await RunAsync($"SELECT toStartOfDay(event_time) AS t, if(country = '', 'XX', country) AS g, count() AS v {source} AND event_type = 'account_created' AND country IN (SELECT country {source} AND event_type = 'account_created' GROUP BY country ORDER BY count() DESC LIMIT {MaxSeries}) GROUP BY t, g ORDER BY t", query.Parameters, report.Errors, cancellationToken), report.NewPlayersSeries);
            ReadSeries(await RunAsync($"SELECT toStartOfDay(event_time) AS t, multiIf(event_type = 'run_started', 'начато', JSONExtractString(event_properties, 'status') = 'completed', 'пройдено', JSONExtractString(event_properties, 'status') = 'failed', 'проиграно', 'брошено') AS g, count() AS v {source} AND event_type IN ('run_started', 'run_finished') GROUP BY t, g ORDER BY t", query.Parameters, report.Errors, cancellationToken), report.RunsSeries);
            ReadSeries(await RunAsync($"SELECT toStartOfDay(event_time) AS t, '% побед' AS g, round(100 * countIf(JSONExtractString(event_properties, 'outcome') = '{WinOutcome}') / count(), 1) AS v {source} AND event_type = 'battle_finished' GROUP BY t ORDER BY t", query.Parameters, report.Errors, cancellationToken), report.BattleWinRateSeries);
            ReadBreakdown(await RunAsync($"SELECT event_type AS g, count() AS events, uniqExact(user_id) AS users, count() AS metric {source} GROUP BY g ORDER BY events DESC LIMIT 20", query.Parameters, report.Errors, cancellationToken), report.TopEvents);
            ReadBreakdown(await RunAsync($"SELECT if(country = '', 'XX', country) AS g, count() AS events, uniqExact(user_id) AS users, uniqExact(user_id) AS metric {source} GROUP BY g ORDER BY users DESC LIMIT 20", query.Parameters, report.Errors, cancellationToken), report.Countries);

            return report;
        }

        public async Task<List<ExperimentGroupStats>> BuildExperimentStatsAsync(string environment, string experimentId, List<string> errors, CancellationToken cancellationToken)
        {
            var stats = new List<ExperimentGroupStats>();
            var table = ResolveTable(environment, errors);

            if (table.Length == 0)
                return stats;

            var parameters = new Dictionary<string, string>(StringComparer.Ordinal) { ["experiment"] = experimentId };
            var activity = await RunAsync(
                $"SELECT group_id AS g, uniqExact(user_id) AS users, round(count() / uniqExact(user_id), 1) AS epu, countIf(event_type = 'run_started') AS runs_started, countIf(event_type = 'run_finished' AND JSONExtractString(event_properties, 'status') = '{CompletedStatus}') AS runs_completed, countIf(event_type = 'battle_finished') AS battles, countIf(event_type = 'battle_finished' AND JSONExtractString(event_properties, 'outcome') = '{WinOutcome}') AS battles_won, round(avgIf(JSONExtractInt(event_properties, 'stage_index'), event_type = 'run_finished'), 2) AS avg_stage FROM {table} WHERE experiment_id = {{experiment:String}} GROUP BY g ORDER BY g",
                parameters,
                errors,
                cancellationToken);
            var retention = await RunAsync(
                $"SELECT f.grp AS g, round(100 * uniqExactIf(f.user_id, a.d = f.d0 + 1) / uniqExact(f.user_id), 1) AS d1, round(100 * uniqExactIf(f.user_id, a.d = f.d0 + 3) / uniqExact(f.user_id), 1) AS d3, round(100 * uniqExactIf(f.user_id, a.d = f.d0 + 7) / uniqExact(f.user_id), 1) AS d7 FROM (SELECT user_id, argMin(group_id, event_time) AS grp, toDate(min(event_time)) AS d0 FROM {table} WHERE experiment_id = {{experiment:String}} GROUP BY user_id) AS f INNER JOIN (SELECT DISTINCT user_id, toDate(event_time) AS d FROM {table}) AS a ON a.user_id = f.user_id GROUP BY g",
                parameters,
                errors,
                cancellationToken);

            if (activity == null)
                return stats;

            for (int i = 0; i < activity.Rows.Count; i++)
            {
                var battles = activity.ReadDouble(i, "battles");
                var runsStarted = activity.ReadDouble(i, "runs_started");
                var group = new ExperimentGroupStats
                {
                    Group = activity.ReadString(i, "g"),
                    Users = (long)activity.ReadDouble(i, "users"),
                    EventsPerUser = activity.ReadDouble(i, "epu"),
                    RunsStarted = (long)runsStarted,
                    RunsCompleted = (long)activity.ReadDouble(i, "runs_completed"),
                    Battles = (long)battles,
                    AverageStageReached = activity.ReadDouble(i, "avg_stage"),
                };

                group.RunCompletionRate = runsStarted <= 0d ? 0d : Math.Round(100d * group.RunsCompleted / runsStarted, 1);
                group.BattleWinRate = battles <= 0d ? 0d : Math.Round(100d * activity.ReadDouble(i, "battles_won") / battles, 1);
                ApplyRetention(retention, group);
                stats.Add(group);
            }

            return stats;
        }

        public async Task<List<EventRow>> LoadPlayerEventsAsync(string environment, string userId, List<string> errors, CancellationToken cancellationToken)
        {
            var events = new List<EventRow>();
            var table = ResolveTable(environment, errors);

            if (table.Length == 0)
                return events;

            var parameters = new Dictionary<string, string>(StringComparer.Ordinal) { ["user_id"] = userId };
            var result = await RunAsync($"SELECT event_time, event_type, user_id, country, experiment_id, group_id, source, app_version, event_properties FROM {table} WHERE user_id = {{user_id:String}} ORDER BY event_time DESC LIMIT 200", parameters, errors, cancellationToken);

            ReadEvents(result, events);

            return events;
        }

        private AnalyticsQuery? Prepare(string environment, AnalyticsFilter filter, List<string> errors)
        {
            var table = ResolveTable(environment, errors);

            if (table.Length == 0)
                return null;

            return _analyticsQueryBuilder.Build(table.Substring(0, table.Length - ".events".Length), filter, _timeProvider.GetUtcNow().UtcDateTime, errors);
        }

        private string ResolveTable(string environment, List<string> errors)
        {
            for (int i = 0; i < _options.Environments.Count; i++)
            {
                var target = _options.Environments[i];

                if (string.Equals(target.Name, environment, StringComparison.Ordinal) == false)
                    continue;

                if (_analyticsQueryBuilder.IsValidDatabase(target.AnalyticsDatabase))
                    return target.AnalyticsDatabase + ".events";

                break;
            }

            errors.Add($"Для окружения {environment} не настроена база аналитики.");

            return string.Empty;
        }

        private async Task<ClickHouseQueryResult?> RunAsync(string sql, Dictionary<string, string> parameters, List<string> errors, CancellationToken cancellationToken)
        {
            var result = await _clickHouseQueryClient.QueryAsync(sql, parameters, cancellationToken);

            if (result.IsSuccess)
                return result;

            if (errors.Contains(result.Error) == false)
                errors.Add(result.Error);

            return null;
        }

        private void ReadSeries(ClickHouseQueryResult? result, List<ChartSeries> series)
        {
            if (result == null)
                return;

            for (int i = 0; i < result.Rows.Count; i++)
            {
                var name = result.ReadString(i, "g");
                var target = FindSeries(series, name);

                target.Points.Add(new ChartPoint { Time = ReadTime(result.ReadString(i, "t")), Value = result.ReadDouble(i, "v") });
            }
        }

        private ChartSeries FindSeries(List<ChartSeries> series, string name)
        {
            for (int i = 0; i < series.Count; i++)
            {
                if (string.Equals(series[i].Name, name, StringComparison.Ordinal))
                    return series[i];
            }

            var created = new ChartSeries { Name = name.Length == 0 ? "(пусто)" : name };

            series.Add(created);

            return created;
        }

        private void ReadBreakdown(ClickHouseQueryResult? result, List<BreakdownRow> rows)
        {
            if (result == null)
                return;

            for (int i = 0; i < result.Rows.Count; i++)
            {
                var group = result.ReadString(i, "g");

                rows.Add(new BreakdownRow
                {
                    Group = group.Length == 0 ? "(пусто)" : group,
                    Events = (long)result.ReadDouble(i, "events"),
                    Users = (long)result.ReadDouble(i, "users"),
                    Metric = result.ReadDouble(i, "metric"),
                });
            }
        }

        private void ReadEvents(ClickHouseQueryResult? result, List<EventRow> rows)
        {
            if (result == null)
                return;

            for (int i = 0; i < result.Rows.Count; i++)
            {
                var experiment = result.ReadString(i, "experiment_id");

                rows.Add(new EventRow
                {
                    Time = ReadTime(result.ReadString(i, "event_time")),
                    EventType = result.ReadString(i, "event_type"),
                    UserId = result.ReadString(i, "user_id"),
                    Country = result.ReadString(i, "country"),
                    Experiment = experiment.Length == 0 ? AnalyticsFilterValues.MasterLabel : experiment + "/" + result.ReadString(i, "group_id"),
                    Source = result.ReadString(i, "source"),
                    AppVersion = result.ReadString(i, "app_version"),
                    Properties = result.ReadString(i, "event_properties"),
                });
            }
        }

        private void ReadStrings(ClickHouseQueryResult? result, string column, List<string> values)
        {
            if (result == null)
                return;

            for (int i = 0; i < result.Rows.Count; i++)
                values.Add(result.ReadString(i, column));
        }

        private void ApplyRetention(ClickHouseQueryResult? retention, ExperimentGroupStats group)
        {
            if (retention == null)
                return;

            for (int i = 0; i < retention.Rows.Count; i++)
            {
                if (string.Equals(retention.ReadString(i, "g"), group.Group, StringComparison.Ordinal) == false)
                    continue;

                group.RetentionD1 = retention.ReadDouble(i, "d1");
                group.RetentionD3 = retention.ReadDouble(i, "d3");
                group.RetentionD7 = retention.ReadDouble(i, "d7");

                return;
            }
        }

        private DateTime ReadTime(string value)
        {
            return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var time) ? time : DateTime.MinValue;
        }
    }
}
