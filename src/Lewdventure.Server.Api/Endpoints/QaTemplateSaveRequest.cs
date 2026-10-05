namespace Server.Api.Endpoints
{
    internal sealed class QaTemplateSaveRequest
    {
        public string TemplateId { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string UserId { get; set; } = string.Empty;
    }
}
