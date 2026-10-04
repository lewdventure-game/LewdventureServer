using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Server.Api.Options;
using Server.Api.Security;
using Server.Infrastructure.Experiments;
using Server.Infrastructure.Mongo;
using Server.Infrastructure.Mongo.Players;
using Server.Infrastructure.Players;

namespace Server.Api.Endpoints
{
    internal sealed class AdminExperimentEndpoints
    {
        private const string ActorPrefix = "admin:";
        private const int DefaultListLimit = 50;
        private const int MaxListLimit = 200;

        public void Map(WebApplication application)
        {
            var adminOptions = application.Services.GetRequiredService<IOptions<AdminOptions>>().Value;
            var mongoOptions = application.Services.GetRequiredService<IOptions<MongoOptions>>().Value;

            if (adminOptions.Enabled == false || mongoOptions.Enabled == false)
                return;

            var group = application.MapGroup(ApiRoutes.AdminExperiments)
                .WithMetadata(new OpsPortOnlyMetadata())
                .RequireAuthorization(SecurityNames.AdminPolicy)
                .RequireRateLimiting(SecurityNames.AdminRateLimitPolicy);

            group.MapGet("/", ListAsync);
            group.MapPost("/", CreateAsync);
            group.MapGet("/player/{userId}", GetPlayerAsync);
            group.MapGet("/{experimentId}", GetAsync);
            group.MapDelete("/{experimentId}", DeleteAsync);
            group.MapGet("/{experimentId}/changes", ListChangesAsync);
            group.MapPost("/{experimentId}/start", StartAsync);
            group.MapPost("/{experimentId}/finish", FinishAsync);
            group.MapPost("/{experimentId}/rollout", RolloutAsync);
            group.MapPost("/{experimentId}/groups/{groupId}/freeze", FreezeAsync);
            group.MapPost("/{experimentId}/groups/{groupId}/remove", RemoveAsync);
        }

        private async Task<IResult> ListAsync(
            HttpContext httpContext,
            [FromQuery] int? limit,
            [FromServices] ExperimentResponseFactory experimentResponseFactory,
            [FromServices] ExperimentService experimentService)
        {
            var effectiveLimit = limit == null || limit.Value <= 0 || MaxListLimit < limit.Value ? DefaultListLimit : limit.Value;
            var summaries = await experimentService.ListAsync(effectiveLimit, httpContext.RequestAborted);
            var responses = new List<ExperimentResponse>(summaries.Count);

            for (int i = 0; i < summaries.Count; i++)
                responses.Add(experimentResponseFactory.Create(summaries[i]));

            return Results.Ok(responses);
        }

        private async Task<IResult> GetAsync(
            HttpContext httpContext,
            string experimentId,
            [FromServices] ExperimentResponseFactory experimentResponseFactory,
            [FromServices] ExperimentService experimentService)
        {
            var summary = await experimentService.GetAsync(experimentId, httpContext.RequestAborted);

            if (summary == null)
                return Results.NotFound(new { error = $"Experiment '{experimentId}' is not found." });

            return Results.Ok(experimentResponseFactory.Create(summary));
        }

        private async Task<IResult> ListChangesAsync(
            HttpContext httpContext,
            string experimentId,
            [FromQuery] int? limit,
            [FromServices] ExperimentService experimentService)
        {
            var effectiveLimit = limit == null || limit.Value <= 0 || MaxListLimit < limit.Value ? DefaultListLimit : limit.Value;
            var changes = await experimentService.ListChangesAsync(experimentId, effectiveLimit, httpContext.RequestAborted);

            return Results.Ok(changes);
        }

        private async Task<IResult> CreateAsync(
            HttpContext httpContext,
            [FromBody] ExperimentCreateRequest? request,
            [FromServices] ExperimentResponseFactory experimentResponseFactory,
            [FromServices] ExperimentService experimentService)
        {
            if (request == null)
                return Results.BadRequest(new { error = "Experiment body is required." });

            var draft = experimentResponseFactory.CreateDraft(request);
            var result = await experimentService.CreateAsync(draft, ReadActor(httpContext), httpContext.RequestAborted);

            return CreateResult(result, experimentResponseFactory);
        }

        private async Task<IResult> DeleteAsync(
            HttpContext httpContext,
            string experimentId,
            [FromServices] ExperimentResponseFactory experimentResponseFactory,
            [FromServices] ExperimentService experimentService)
        {
            var result = await experimentService.DeleteDraftAsync(experimentId, ReadActor(httpContext), httpContext.RequestAborted);

            return CreateResult(result, experimentResponseFactory);
        }

        private async Task<IResult> StartAsync(
            HttpContext httpContext,
            string experimentId,
            [FromBody] ExperimentActionRequest? request,
            [FromServices] ExperimentResponseFactory experimentResponseFactory,
            [FromServices] ExperimentService experimentService)
        {
            var result = await experimentService.StartAsync(experimentId, ReadActor(httpContext), ReadReason(request), httpContext.RequestAborted);

            return CreateResult(result, experimentResponseFactory);
        }

        private async Task<IResult> FinishAsync(
            HttpContext httpContext,
            string experimentId,
            [FromBody] ExperimentActionRequest? request,
            [FromServices] ExperimentResponseFactory experimentResponseFactory,
            [FromServices] ExperimentService experimentService)
        {
            var result = await experimentService.FinishAsync(experimentId, ReadActor(httpContext), ReadReason(request), httpContext.RequestAborted);

            return CreateResult(result, experimentResponseFactory);
        }

        private async Task<IResult> RolloutAsync(
            HttpContext httpContext,
            string experimentId,
            [FromBody] ExperimentActionRequest? request,
            [FromServices] ExperimentResponseFactory experimentResponseFactory,
            [FromServices] ExperimentService experimentService)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.GroupId))
                return Results.BadRequest(new { error = "groupId is required." });

            var result = await experimentService.RolloutAsync(experimentId, request.GroupId, ReadActor(httpContext), ReadReason(request), httpContext.RequestAborted);

            return CreateResult(result, experimentResponseFactory);
        }

        private async Task<IResult> FreezeAsync(
            HttpContext httpContext,
            string experimentId,
            string groupId,
            [FromBody] ExperimentActionRequest? request,
            [FromServices] ExperimentResponseFactory experimentResponseFactory,
            [FromServices] ExperimentService experimentService)
        {
            var result = await experimentService.FreezeGroupAsync(experimentId, groupId, ReadActor(httpContext), ReadReason(request), httpContext.RequestAborted);

            return CreateResult(result, experimentResponseFactory);
        }

        private async Task<IResult> RemoveAsync(
            HttpContext httpContext,
            string experimentId,
            string groupId,
            [FromBody] ExperimentActionRequest? request,
            [FromServices] ExperimentResponseFactory experimentResponseFactory,
            [FromServices] ExperimentService experimentService)
        {
            var result = await experimentService.RemoveGroupAsync(experimentId, groupId, ReadActor(httpContext), ReadReason(request), httpContext.RequestAborted);

            return CreateResult(result, experimentResponseFactory);
        }

        private async Task<IResult> GetPlayerAsync(
            HttpContext httpContext,
            string userId,
            [FromServices] ExperimentRegistry experimentRegistry,
            [FromServices] PlayerConfigVersionResolver playerConfigVersionResolver,
            [FromServices] UserRepository userRepository)
        {
            var assignment = await userRepository.GetExperimentAsync(userId, httpContext.RequestAborted);
            var response = new ExperimentPlayerResponse
            {
                UserId = userId,
                ConfigVersion = await playerConfigVersionResolver.ResolveAsync(userId, httpContext.RequestAborted),
            };

            if (assignment != null)
            {
                response.ExperimentId = assignment.ExperimentId;
                response.GroupId = assignment.GroupId;
                response.AssignedAt = assignment.AssignedAt;
                response.Country = assignment.Country;
                response.IsActive = experimentRegistry.TryGetActiveGroup(assignment.ExperimentId, assignment.GroupId, out _);
            }

            return Results.Ok(response);
        }

        private IResult CreateResult(ExperimentOperationResult result, ExperimentResponseFactory experimentResponseFactory)
        {
            switch (result.Status)
            {
                case ExperimentOperationStatus.Ok:
                    return result.Experiment == null
                        ? Results.Ok(new { warnings = result.Warnings })
                        : Results.Ok(new { experiment = experimentResponseFactory.Create(result.Experiment), warnings = result.Warnings });
                case ExperimentOperationStatus.NotFound:
                    return Results.NotFound(new { errors = result.Errors });
                case ExperimentOperationStatus.Conflict:
                    return Results.Conflict(new { errors = result.Errors });
                default:
                    return Results.BadRequest(new { errors = result.Errors, warnings = result.Warnings });
            }
        }

        private string ReadActor(HttpContext httpContext)
        {
            return ActorPrefix + (httpContext.Connection.RemoteIpAddress == null ? "unknown" : httpContext.Connection.RemoteIpAddress.ToString());
        }

        private string ReadReason(ExperimentActionRequest? request)
        {
            return request == null ? string.Empty : request.Reason.Trim();
        }
    }
}
