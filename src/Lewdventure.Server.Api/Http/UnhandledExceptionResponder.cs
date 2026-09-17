using Microsoft.AspNetCore.Diagnostics;

namespace Server.Api.Http
{
    internal sealed class UnhandledExceptionResponder
    {
        private const string ResponseBody = "{\"error\": \"Внутренняя ошибка сервера\"}";

        public void Configure(IApplicationBuilder applicationBuilder)
        {
            applicationBuilder.Run(RespondAsync);
        }

        private async Task RespondAsync(HttpContext context)
        {
            var response = context.Response;

            response.StatusCode = StatusCodes.Status500InternalServerError;
            response.ContentType = "application/json";

            await response.WriteAsync(ResponseBody);

            var logger = context.RequestServices.GetRequiredService<ILogger<UnhandledExceptionResponder>>();
            var exceptionFeature = context.Features.Get<IExceptionHandlerFeature>();

            logger.LogError(exceptionFeature?.Error, "[Error] unhandled exception");
        }
    }
}
