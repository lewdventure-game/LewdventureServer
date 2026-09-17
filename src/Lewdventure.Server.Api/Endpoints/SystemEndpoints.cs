namespace Server.Api.Endpoints
{
    internal sealed class SystemEndpoints
    {
        public void Map(WebApplication application)
        {
            application.MapGet(ApiRoutes.Root, GetRoot);
            application.MapGet(ApiRoutes.Ping, GetPing);
        }

        private string GetRoot()
        {
            return "Hello World!";
        }

        private IResult GetPing()
        {
            return Results.Ok(new
            {
                message = "Server is running",
                time = DateTime.UtcNow,
            });
        }
    }
}
