using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Server.Api.Http;
using Server.Api.Metrics;
using Server.Api.Options;
using Server.Api.Security;
using Server.Battles;

namespace Server.Api.Endpoints
{
    internal sealed class BattleEndpoints
    {
        private const string InvalidJsonMessage = "Request body is not valid JSON.";

        public void Map(WebApplication application)
        {
            var requestLimits = application.Services.GetRequiredService<IOptions<RequestLimitsOptions>>().Value;
            var concurrencyLimiter = application.Services.GetRequiredService<BattleConcurrencyLimiter>();
            var metricsFilter = application.Services.GetRequiredService<BattleMetricsFilter>();
            var readyFilter = application.Services.GetRequiredService<GameConfigReadyFilter>();
            var sizeLimit = new RequestSizeLimitAttribute(requestLimits.BattleMaxRequestBodyBytes);

            application.MapPost(ApiRoutes.SimulateBattle, SimulateAsync)
                .WithMetadata(sizeLimit)
                .RequireRateLimiting(SecurityNames.BattleRateLimitPolicy)
                .AddEndpointFilter(metricsFilter.InvokeAsync)
                .AddEndpointFilter(readyFilter.InvokeAsync)
                .AddEndpointFilter(concurrencyLimiter.InvokeAsync)
                .Accepts<BattleSimulationData>("application/json")
                .Produces<BattleScriptResponse>(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status400BadRequest);

            application.MapPost(ApiRoutes.ReplayBattle, ReplayAsync)
                .WithMetadata(sizeLimit)
                .RequireRateLimiting(SecurityNames.BattleRateLimitPolicy)
                .AddEndpointFilter(metricsFilter.InvokeAsync)
                .AddEndpointFilter(readyFilter.InvokeAsync)
                .AddEndpointFilter(concurrencyLimiter.InvokeAsync)
                .Accepts<BattleReplayData>("application/json")
                .Produces<BattleScriptResponse>(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status400BadRequest);
        }

        private async Task<IResult> SimulateAsync(
            HttpRequest httpRequest,
            [FromServices] JsonSerializerSettings serializerSettings,
            [FromServices] IBattleSimulationValidator validator,
            [FromServices] BattleSimulatorService simulator)
        {
            var body = await ReadBodyAsync(httpRequest);
            BattleSimulationData? request;

            try
            {
                request = JsonConvert.DeserializeObject<BattleSimulationData>(body, serializerSettings);
            }
            catch (JsonException) when (IsMalformedJson(body))
            {
                return Results.BadRequest(new { error = InvalidJsonMessage });
            }

            if (request == null)
                return Results.BadRequest(new { error = "Request body is required." });

            if (validator.TryValidate(request, out var errorMessage) == false)
                return Results.BadRequest(new { error = errorMessage });

            var script = simulator.Simulate(request);
            var responseJson = JsonConvert.SerializeObject(script, serializerSettings);

            return Results.Content(responseJson, "application/json");
        }

        private async Task<IResult> ReplayAsync(
            HttpRequest httpRequest,
            [FromServices] JsonSerializerSettings serializerSettings,
            [FromServices] IBattleSimulationValidator validator,
            [FromServices] BattleSimulatorService simulator)
        {
            var body = await ReadBodyAsync(httpRequest);
            BattleReplayData? request;

            try
            {
                request = JsonConvert.DeserializeObject<BattleReplayData>(body, serializerSettings);
            }
            catch (JsonException) when (IsMalformedJson(body))
            {
                return Results.BadRequest(new { error = InvalidJsonMessage });
            }

            if (request == null)
                return Results.BadRequest(new { error = "Request body is required." });

            if (validator.TryValidate(request, out var errorMessage) == false)
                return Results.BadRequest(new { error = errorMessage });

            var script = simulator.Replay(request);
            var responseJson = JsonConvert.SerializeObject(script, serializerSettings);

            return Results.Content(responseJson, "application/json");
        }

        private bool IsMalformedJson(string body)
        {
            try
            {
                JToken.Parse(body);

                return false;
            }
            catch (JsonReaderException)
            {
                return true;
            }
        }

        private async Task<string> ReadBodyAsync(HttpRequest httpRequest)
        {
            using var streamReader = new StreamReader(httpRequest.Body);

            return await streamReader.ReadToEndAsync();
        }
    }
}
