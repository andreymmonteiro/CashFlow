using ConsolidationService.Application.Commands;
using ConsolidationService.Application.Queries;
using ConsolidationService.Infrastructure.DI;
using ConsolidationService.Infrastructure.Messaging.Consumers;
using ConsolidationService.Infrastructure.Options;
using ConsolidationService.Presentation.Dtos.Request;
using ConsolidationService.Presentation.Dtos.Response;
using Microsoft.AspNetCore.Diagnostics;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace ConsolidationService;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddLogging();

        if (!builder.Environment.IsEnvironment("Testing"))
        {
            var factory = builder.Services
                .AddRabbitMq(builder.Configuration["RabbitMq:Host"]);

            var mongoDbOptions = builder.Configuration.GetSection(MongoDbOptions.SectionName).Get<MongoDbOptions>();

            builder.Services
                .AddMongoDb(mongoDbOptions)
                .AddEventStore(builder.Configuration["EventStoreDb:ConnectionString"]);

            await RegisterConnection(factory);
        }

        builder.Services.AddScoped<ICommandHandler<CreateConsolidationCommand, long>, CreateConsolidationCommandHandler>();

        builder.Services.AddScoped<IQueryHandler<DailyConsolidationRequest, DailyConsolidationResponse>, DailyConsolidationQueryHandler>();

        builder.Services.AddExceptionHandler(options =>
        {
            options.AllowStatusCode404Response = false;

            options.ExceptionHandler = async context =>
            {
                var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
                                                    .CreateLogger("GlobalExceptionHandler");

                var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;

                if (exception is BadHttpRequestException badHttpRequest)
                {
                    // handle bad HTTP request
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        Message = $"Bad request: {badHttpRequest?.InnerException?.Message ?? badHttpRequest?.Message}"
                    });
                    return;
                }

                logger.LogError(exception, "An unhandled exception occurred.");

                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                context.Response.ContentType = "application/json";

                var errorResponse = new
                {
                    Message = "An unexpected error occurred. Please try again later."
                };

                await context.Response.WriteAsJsonAsync(errorResponse);
            };
        });

        builder.Services.AddHostedService<CreatedTransactionConsumer>();

        builder.Services.AddEndpointsApiExplorer();

        builder.Services.AddSwaggerGen();

        var app = builder.Build();

        app.UseSwagger();
        app.UseSwaggerUI();

        app.UseHttpsRedirection();

        app.MapPost("/api/consolidation/daily/query",
            async (
                DailyConsolidationRequest request,
                IQueryHandler<DailyConsolidationRequest, DailyConsolidationResponse> handler,
                CancellationToken cancellationToken
            ) =>
            {
                var result = await handler.HandleAsync(request, cancellationToken);

                return Results.Ok(result);
            });

        app.UseExceptionHandler();

        app.Run();


        async Task RegisterConnection(IConnectionFactory factory)
        {
            var shouldRetry = true;
            var retries = 0;

            var brokerConnection = default(IConnection);

            while (shouldRetry)
            {
                try
                {
                    var connection = await factory.CreateConnectionAsync();

                    brokerConnection = connection;
                    shouldRetry = false;
                }
                catch(BrokerUnreachableException e)
                {
                    retries++;

                    shouldRetry = retries < 3;

                    await Task.Delay(3000);
                }
            }
            

            builder.Services.AddSingleton(brokerConnection);
        }
    }
}