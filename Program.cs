using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Server.Battles;
using Server.Bonuses;
using Server.Common;
using Server.Configs;
using Server.Entities;
using Server.Equipments;
using Server.Perks;
using Server.Services;
using Server.Statuses;
using Server.Stories;

namespace Server
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var url = $"{UrlConfig.LocalHost}:{ServerConfig.Port}";
            var builder = CreateBuilder(args, url);

            BuildWebApplication(builder, url);
        }

        private static WebApplicationBuilder CreateBuilder(string[] args, string url)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.WebHost.UseUrls(url);

            ConfigureServices(builder.Services);

            return builder;
        }

        private static void BuildWebApplication(WebApplicationBuilder builder, string url)
        {
            var application = builder.Build();

            ConfigureMiddleware(application);
            ConfigureEndpoints(application);

            Console.WriteLine($"Battle Server started on {url}");

            application.Run();
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            // 1. Инфраструктура
            services
                .AddHttpClient()
                .AddEndpointsApiExplorer()
                .AddSwaggerGen();

            // 2. Бизнес-логика
            services
                .AddSingleton<IConfigDistributor, ConfigDistributor>()
                .AddSingleton<IGameConfigService, GameConfigService>()
                .AddSingleton<IConstantsMapperManager, ConstantsMapperManager>()
                .AddSingleton<IBonusMapperManager, BonusMapperManager>()
                .AddSingleton<ICharacterMapperManager, CharacterMapperManager>()
                .AddSingleton<IEnemyMapperManager, EnemyMapperManager>()
                .AddSingleton<IEquipmentMapperManager, EquipmentMapperManager>()
                .AddSingleton<IExperienceLevelPatternMapperManager, ExperienceLevelPatternMapperManager>()
                .AddSingleton<IPerkGroupMapperManager, PerkGroupMapperManager>()
                .AddSingleton<IPerkMapperManager, PerkMapperManager>()
                .AddSingleton<IStatusMapperManager, StatusMapperManager>()
                .AddSingleton<IStoryEventMapperManager, StoryEventMapperManager>()
                .AddSingleton<IStoryLevelMapperManager, StoryLevelMapperManager>()
                .AddSingleton<IStoryStageMapperManager, StoryStageMapperManager>()
                .AddSingleton<ISummonLevelMapperManager, SummonLevelMapperManager>()
                .AddSingleton<ISummonMapperManager, SummonMapperManager>()
                .AddSingleton<IBattleStatusSimulator, BattleStatusSimulator>()
                .AddSingleton<BattleSimulatorService>();

            // 3. Контроллеры
            services
                .AddControllers()
                .ConfigureApiBehaviorOptions(options =>
                {
                    options.InvalidModelStateResponseFactory = context => new BadRequestObjectResult(context.ModelState);
                })
                .AddNewtonsoftJson(options =>
                {
                    var serializerSettings = options.SerializerSettings;
                    serializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
                    serializerSettings.NullValueHandling = NullValueHandling.Ignore;
                    serializerSettings.ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor;
                });
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
                    logger.LogError(exceptionFeature?.Error, "Unhandled exception");
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

            // Основной POST запрос для симуляции боя
            app.MapPost(UrlConfig.SimulateBattle, (
                BattleSimulationData request,
                [FromServices] BattleSimulatorService simulator) =>
            {
                var script = simulator.Simulate(request);
                return Results.Ok(script);
            });
        }
    }
}
