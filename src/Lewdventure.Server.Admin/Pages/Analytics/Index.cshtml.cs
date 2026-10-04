using System.Globalization;
using Server.Admin.Analytics;
using Server.Admin.Backend;

namespace Server.Admin.Pages.Analytics
{
    internal sealed class IndexModel : AdminPageModel
    {
        private const int DefaultDays = 7;
        private const int MaxPresetDays = 400;

        private readonly AnalyticsReportService _analyticsReportService;
        private readonly SvgChartBuilder _svgChartBuilder;
        private readonly TimeProvider _timeProvider;

        public IndexModel(
            AdminAuditLog auditLog,
            AdminEnvironmentSelector environmentSelector,
            GameAdminClient gameAdminClient,
            AnalyticsReportService analyticsReportService,
            SvgChartBuilder svgChartBuilder,
            TimeProvider timeProvider)
            : base(auditLog, environmentSelector, gameAdminClient)
        {
            _analyticsReportService = analyticsReportService;
            _svgChartBuilder = svgChartBuilder;
            _timeProvider = timeProvider;
        }

        public AnalyticsFilter Filter { get; } = new();

        public int Days { get; private set; }

        public OverviewReport Report { get; private set; } = new();

        public SvgChart ActivePlayersChart { get; private set; } = new();

        public SvgChart NewPlayersChart { get; private set; } = new();

        public SvgChart RunsChart { get; private set; } = new();

        public SvgChart WinRateChart { get; private set; } = new();

        public async Task OnGetAsync(int? days, string? from, string? to)
        {
            var today = _timeProvider.GetUtcNow().UtcDateTime.Date;

            if (string.IsNullOrEmpty(from) && string.IsNullOrEmpty(to))
            {
                Days = days == null || days.Value <= 0 || MaxPresetDays < days.Value ? DefaultDays : days.Value;
                Filter.From = today.AddDays(1 - Days).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                Filter.To = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }
            else
            {
                Filter.From = from ?? string.Empty;
                Filter.To = to ?? string.Empty;
            }

            Report = await _analyticsReportService.BuildOverviewAsync(CurrentEnvironment, Filter, HttpContext.RequestAborted);
            ActivePlayersChart = _svgChartBuilder.Build(Report.ActivePlayersSeries, false);
            NewPlayersChart = _svgChartBuilder.Build(Report.NewPlayersSeries, false);
            RunsChart = _svgChartBuilder.Build(Report.RunsSeries, false);
            WinRateChart = _svgChartBuilder.Build(Report.BattleWinRateSeries, false);
        }
    }
}
