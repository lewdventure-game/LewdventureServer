using Server.Infrastructure.Mongo.Players;
using Server.Infrastructure.Mongo.Qa;

namespace Server.Api.Endpoints
{
    internal sealed class QaBugReportResponse
    {
        public string Format { get; set; } = "lewdventure-bug-report";

        public int FormatVersion { get; set; } = 1;

        public DateTime GeneratedAt { get; set; }

        public string GeneratedBy { get; set; } = string.Empty;

        public string Environment { get; set; } = string.Empty;

        public string ServerVersion { get; set; } = string.Empty;

        public string MasterConfigVersion { get; set; } = string.Empty;

        public QaAccountResponse? Account { get; set; }

        public string ExperimentId { get; set; } = string.Empty;

        public string GroupId { get; set; } = string.Empty;

        public PlayerProfileDocument? Profile { get; set; }

        public QaRunResponse? ActiveRun { get; set; }

        public List<QaRunResponse> RecentRuns { get; set; } = new();

        public List<PlayerLedgerDocument> Ledger { get; set; } = new();

        public List<RequestTraceDocument> Requests { get; set; } = new();

        public List<ServerErrorDocument> Errors { get; set; } = new();
    }
}
