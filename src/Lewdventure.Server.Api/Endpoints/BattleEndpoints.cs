using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Server.Battles;

namespace Server.Api.Endpoints
{
    internal sealed class BattleEndpoints
    {
        public void Map(WebApplication application)
        {
            application.MapPost(ApiRoutes.SimulateBattle, SimulateAsync)
                .Accepts<BattleSimulationData>("application/json")
                .Produces<BattleScriptResponse>(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status400BadRequest);

            application.MapPost(ApiRoutes.ReplayBattle, ReplayAsync)
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
            var request = JsonConvert.DeserializeObject<BattleSimulationData>(body, serializerSettings);

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
            var request = JsonConvert.DeserializeObject<BattleReplayData>(body, serializerSettings);

            if (request == null)
                return Results.BadRequest(new { error = "Request body is required." });

            if (validator.TryValidate(request, out var errorMessage) == false)
                return Results.BadRequest(new { error = errorMessage });

            var script = simulator.Replay(request);
            var responseJson = JsonConvert.SerializeObject(script, serializerSettings);

            return Results.Content(responseJson, "application/json");
        }

        private async Task<string> ReadBodyAsync(HttpRequest httpRequest)
        {
            using var streamReader = new StreamReader(httpRequest.Body);

            return await streamReader.ReadToEndAsync();
        }
    }
}
