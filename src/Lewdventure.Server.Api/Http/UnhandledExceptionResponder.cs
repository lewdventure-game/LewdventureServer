using Microsoft.AspNetCore.Diagnostics;

namespace Server.Api.Http
{
    internal sealed class UnhandledExceptionResponder
    {
        private const string InternalErrorBody = "{\"error\": \"Внутренняя ошибка сервера\"}";
        private const string PayloadTooLargeBody = "{\"error\":\"Request body is too large.\"}";
        private const string BadRequestBody = "{\"error\":\"Bad request.\"}";

        public void Configure(IApplicationBuilder applicationBuilder)
        {
            applicationBuilder.Run(RespondAsync);
        }

        private async Task RespondAsync(HttpContext context)
        {
            var response = context.Response;
            var logger = context.RequestServices.GetRequiredService<ILogger<UnhandledExceptionResponder>>();
            var exceptionFeature = context.Features.Get<IExceptionHandlerFeature>();
            var exception = exceptionFeature?.Error;

            response.ContentType = "application/json";

            if (exception is BadHttpRequestException badHttpRequestException)
            {
                response.StatusCode = badHttpRequestException.StatusCode;

                await response.WriteAsync(badHttpRequestException.StatusCode == StatusCodes.Status413PayloadTooLarge ? PayloadTooLargeBody : BadRequestBody);

                logger.LogWarning("[Security] rejected request status = {StatusCode} path = {Path} reason = {Reason}", badHttpRequestException.StatusCode, context.Request.Path, badHttpRequestException.Message);

                return;
            }

            response.StatusCode = StatusCodes.Status500InternalServerError;

            await response.WriteAsync(InternalErrorBody);

            logger.LogError(exception, "[Error] unhandled exception");
        }
    }
}
