using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Server.Battles;
using Server.Bonuses;
using Server.Configs;
using Server.Services;

namespace Server
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var url = $"{UrlConfig.LocalHost}:{ServerConfig.Port}";
            var builder = CreateBuilder(args, url);

            await BuildWebApplication(builder, url);
        }

        private static WebApplicationBuilder CreateBuilder(string[] args, string url)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.WebHost.UseUrls(url);

            ConfigureServices(builder.Services);

            return builder;
        }

        private static async Task BuildWebApplication(WebApplicationBuilder builder, string url)
        {
            var application = builder.Build();

            ConfigureMiddleware(application);
            ConfigureEndpoints(application);

            await SyncConfigsOnStartup(application);

            Console.WriteLine($"Battle Server started on {url}");

            await application.RunAsync();
        }

        private static async Task SyncConfigsOnStartup(WebApplication application)
        {
            var configService = application.Services.GetRequiredService<IGameConfigService>();
            var environment = application.Services.GetRequiredService<IHostEnvironment>();
            var logger = application.Services.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(Program));

            logger.LogInformation("[Config] startup sync begin (same path as POST /api/config/update)");

            var (success, message) = await configService.UpdateAllConfigsAsync(environment.IsDevelopment());

            if (success == false)
            {
                logger.LogError($"[Config] startup sync failed: {message}");

                return;
            }

            logger.LogInformation($"[Config] startup sync ok: {message}");
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            // 1. Инфраструктура
            services
                .AddHttpClient()
                .AddEndpointsApiExplorer()
                .AddSwaggerGen();

            // 2. Бизнес-логика — configs only via IConfigDistributor (managers live inside it)
            services
                .AddSingleton<IConfigDistributor, ConfigDistributor>()
                .AddSingleton<IGameConfigService, GameConfigService>()
                .AddSingleton<IBonusWorkModeParser, BonusWorkModeParser>()
                .AddSingleton<IBattleAttackService, BattleAttackService>()
                .AddSingleton<IBattleBonusService, BattleBonusService>()
                .AddSingleton<IBattleCommandFactory, BattleCommandFactory>()
                .AddSingleton<IBattleParameterParser, BattleParameterParser>()
                .AddSingleton<IBattlePerkSimulator, BattlePerkSimulator>()
                .AddSingleton<IBattleRewardParser, BattleRewardParser>()
                .AddSingleton<IBattleRewardService, BattleRewardService>()
                .AddSingleton<IBattleScriptBuilder, BattleScriptBuilder>()
                .AddSingleton<IBattleSimulationValidator, BattleSimulationValidator>()
                .AddSingleton<IBattleSkillSimulator, BattleSkillSimulator>()
                .AddSingleton<IBattleStatusSimulator, BattleStatusSimulator>()
                .AddSingleton<IBattleSummonSimulator, BattleSummonSimulator>()
                .AddSingleton<ICharacteristicCalculator, CharacteristicCalculator>()
                .AddSingleton<IPerkFactory, PerkFactory>()
                .AddSingleton<ISkillFactory, SkillFactory>()
                .AddSingleton<IStatusParametersParser, StatusParametersParser>()
                .AddSingleton<IUnitStateBuilder, UnitStateBuilder>()
                .AddSingleton<BattleSimulatorService>();

            // 3. JSON — Newtonsoft канон; Minimal API по умолчанию жрёт System.Text.Json, его для battle не используем.
            services.AddSingleton(CreateNewtonsoftSerializerSettings());

            services
                .AddControllers()
                .ConfigureApiBehaviorOptions(options =>
                {
                    options.InvalidModelStateResponseFactory = context => new BadRequestObjectResult(context.ModelState);
                })
                .AddNewtonsoftJson(options =>
                {
                    ApplyNewtonsoftSerializerSettings(options.SerializerSettings);
                });
        }

        private static JsonSerializerSettings CreateNewtonsoftSerializerSettings()
        {
            var serializerSettings = new JsonSerializerSettings();
            ApplyNewtonsoftSerializerSettings(serializerSettings);

            return serializerSettings;
        }

        private static void ApplyNewtonsoftSerializerSettings(JsonSerializerSettings serializerSettings)
        {
            serializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
            serializerSettings.NullValueHandling = NullValueHandling.Ignore;
            serializerSettings.ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor;
            serializerSettings.Converters.Add(new TeamSnapshotJsonConverter());
            serializerSettings.Converters.Add(new UnitSnapshotJsonConverter());
            serializerSettings.Converters.Add(new EquipmentSnapshotJsonConverter());
        }

        private static void ConfigureMiddleware(WebApplication application)
        {
            // Глобальная обработка ошибок
            application.UseExceptionHandler(exceptionHandlerApplication =>
            {
                exceptionHandlerApplication.Run(async context =>
                {
                    var response = context.Response;
                    response.StatusCode = StatusCodes.Status500InternalServerError;
                    response.ContentType = "application/json";

                    await response.WriteAsync("{\"error\": \"Внутренняя ошибка сервера\"}");

                    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
                    var exceptionFeature = context.Features.Get<IExceptionHandlerFeature>();
                    logger.LogError(exceptionFeature?.Error, "[Error] unhandled exception");
                });
            });

            if (application.Environment.IsDevelopment())
            {
                application.UseSwagger();
                application.UseSwaggerUI(swaggerUiOptions =>
                    swaggerUiOptions.SwaggerEndpoint("/swagger/v1/swagger.json", "Lewdventure Server API v1"));
            }
        }

        private static void ConfigureEndpoints(WebApplication app)
        {
            // Базовые эндпоинты
            app.MapGet(UrlConfig.Root, () => "Hello World!");

            app.MapGet(UrlConfig.Ping, () =>
            {
                return Results.Ok(new
                {
                    message = "Server is running",
                    time = DateTime.UtcNow,
                });
            });

            // Эндпоинт обновления конфигов
            app.MapPost(UrlConfig.UpdateConfig, async (
                [FromHeader(Name = "X-Config-Secret")] string? secretKey,
                [FromServices] IGameConfigService configService,
                IHostEnvironment environment) =>
            {
                const string expectedKey = "1";//"MySuperSecretDevKey123"; // В будущем вынести в appsettings.json

                if (secretKey != expectedKey)
                    return Results.Unauthorized();

                var isDev = environment.IsDevelopment();
                var (success, message) = await configService.UpdateAllConfigsAsync(isDev);

                return success
                    ? Results.Ok(new { status = "success", message })
                    : Results.Problem(detail: message, statusCode: 500);
            });

            // Battle body/response — только Newtonsoft. Accepts/Produces нужны Swagger'у (параметр HttpRequest он не документирует).
            app.MapPost(UrlConfig.SimulateBattle, async (
                HttpRequest httpRequest,
                [FromServices] JsonSerializerSettings serializerSettings,
                [FromServices] IBattleSimulationValidator validator,
                [FromServices] BattleSimulatorService simulator) =>
            {
                string body;

                using (var streamReader = new StreamReader(httpRequest.Body))
                    body = await streamReader.ReadToEndAsync();

                var request = JsonConvert.DeserializeObject<BattleSimulationData>(body, serializerSettings);

                if (request == null)
                    return Results.BadRequest(new { error = "Request body is required." });

                if (validator.TryValidate(request, out var errorMessage) == false)
                    return Results.BadRequest(new { error = errorMessage });

                var script = simulator.Simulate(request);
                var responseJson = JsonConvert.SerializeObject(script, serializerSettings);

                return Results.Content(responseJson, "application/json");
            })
            .Accepts<BattleSimulationData>("application/json")
            .Produces<BattleScriptResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);

            app.MapPost(UrlConfig.ReplayBattle, async (
                HttpRequest httpRequest,
                [FromServices] JsonSerializerSettings serializerSettings,
                [FromServices] IBattleSimulationValidator validator,
                [FromServices] BattleSimulatorService simulator) =>
            {
                string body;

                using (var streamReader = new StreamReader(httpRequest.Body))
                    body = await streamReader.ReadToEndAsync();

                var request = JsonConvert.DeserializeObject<BattleReplayData>(body, serializerSettings);

                if (request == null)
                    return Results.BadRequest(new { error = "Request body is required." });

                if (validator.TryValidate(request, out var errorMessage) == false)
                    return Results.BadRequest(new { error = errorMessage });

                var script = simulator.Replay(request);
                var responseJson = JsonConvert.SerializeObject(script, serializerSettings);

                return Results.Content(responseJson, "application/json");
            })
            .Accepts<BattleReplayData>("application/json")
            .Produces<BattleScriptResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);
        }
    }
}
