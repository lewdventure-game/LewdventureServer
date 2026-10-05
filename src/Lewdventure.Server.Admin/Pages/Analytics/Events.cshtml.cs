using Microsoft.AspNetCore.Mvc;
using Server.Admin.Analytics;
using Server.Admin.Backend;
using Server.Admin.Backend.Models;

namespace Server.Admin.Pages.Analytics
{
    internal sealed class EventsModel : AdminPageModel
    {
        private const string UserIdPrefix = "usr_";

        private readonly AnalyticsReportService _analyticsReportService;
        private readonly SvgChartBuilder _svgChartBuilder;

        public EventsModel(
            AdminAuditLog auditLog,
            AdminEnvironmentSelector environmentSelector,
            GameAdminClient gameAdminClient,
            AnalyticsReportService analyticsReportService,
            SvgChartBuilder svgChartBuilder)
            : base(auditLog, environmentSelector, gameAdminClient)
        {
            _analyticsReportService = analyticsReportService;
            _svgChartBuilder = svgChartBuilder;
        }

        [BindProperty(SupportsGet = true)]
        public AnalyticsFilter Filter { get; set; } = new();

        public AnalyticsReport Report { get; private set; } = new();

        public SvgChart Chart { get; private set; } = new();

        public List<ExperimentModel> Experiments { get; private set; } = new();

        public string PlayerNote { get; private set; } = string.Empty;

        public bool HasExtraFilters => Filter.Player.Length != 0
            || Filter.UserId.Length != 0
            || Filter.ExperimentId.Length != 0
            || Filter.GroupId.Length != 0
            || Filter.Country.Length != 0
            || Filter.Source.Length != 0
            || Filter.PropertyKey.Length != 0;

        public string MetricTitle
        {
            get
            {
                switch (Filter.Metric)
                {
                    case AnalyticsFilterValues.MetricUsers:
                        return "Разных игроков";
                    case AnalyticsFilterValues.MetricSum:
                        return "Сумма " + Filter.MetricKey;
                    case AnalyticsFilterValues.MetricAverage:
                        return "Среднее " + Filter.MetricKey;
                    default:
                        return "Сколько раз";
                }
            }
        }

        public async Task OnGetAsync()
        {
            await ResolvePlayerAsync();

            var excludedUserIds = Filter.IncludeQa || Filter.UserId.Length != 0 || Filter.DeviceId.Length != 0 ? new List<string>() : await LoadQaUserIdsAsync();

            Report = await _analyticsReportService.BuildEventReportAsync(CurrentEnvironment, Filter, excludedUserIds, HttpContext.RequestAborted);
            Chart = _svgChartBuilder.Build(Report.Series, Filter.Step == AnalyticsFilterValues.StepHour);

            var experiments = await LoadAsync<List<ExperimentModel>>("/admin/experiments?limit=50");

            if (experiments != null)
                Experiments = experiments;
        }

        private async Task ResolvePlayerAsync()
        {
            var query = Filter.Player.Trim();

            if (query.Length == 0)
                query = Filter.UserId.Trim();

            Filter.Player = query;
            Filter.UserId = string.Empty;
            Filter.DeviceId = string.Empty;

            if (query.Length == 0)
                return;

            if (query.StartsWith(UserIdPrefix, StringComparison.Ordinal))
            {
                Filter.UserId = query;

                return;
            }

            var matches = await LoadAsync<List<QaMatchModel>>("/admin/qa/find?query=" + Escape(query));

            if (matches != null && matches.Count == 1)
            {
                Filter.UserId = matches[0].UserId;
                PlayerNote = $"Найден по {(matches[0].MatchedBy == "alias" ? "псевдониму" : "deviceId")}: {matches[0].UserId}";

                return;
            }

            Filter.DeviceId = query;
            PlayerNote = "Аккаунт с таким deviceId или псевдонимом не найден, показаны события с этим deviceId.";
        }
    }
}
