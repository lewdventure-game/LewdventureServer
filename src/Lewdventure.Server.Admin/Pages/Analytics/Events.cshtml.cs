using Microsoft.AspNetCore.Mvc;
using Server.Admin.Analytics;
using Server.Admin.Backend;
using Server.Admin.Backend.Models;

namespace Server.Admin.Pages.Analytics
{
    internal sealed class EventsModel : AdminPageModel
    {
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

        public bool HasExtraFilters => Filter.UserId.Length != 0
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
            Report = await _analyticsReportService.BuildEventReportAsync(CurrentEnvironment, Filter, HttpContext.RequestAborted);
            Chart = _svgChartBuilder.Build(Report.Series, Filter.Step == AnalyticsFilterValues.StepHour);

            var experiments = await LoadAsync<List<ExperimentModel>>("/admin/experiments?limit=50");

            if (experiments != null)
                Experiments = experiments;
        }
    }
}
