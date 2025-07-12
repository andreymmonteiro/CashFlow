using ConsolicationService.Application.Commands;
using ConsolicationService.Application.Queries;
using ConsolicationService.Infrastructure.DI;
using ConsolicationService.Infrastructure.Messaging.Channels;
using ConsolicationService.Infrastructure.Messaging.Consumers;
using ConsolicationService.Presentation.Dtos.Request;
using ConsolicationService.Presentation.Dtos.Response;
using Microsoft.AspNetCore.Diagnostics;
using RabbitMQ.Client;

namespace ConsolicationService;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddLogging();

        var factory = builder.Services
            .AddRabbitMq();

        builder.Services
            .AddMongoDb()
            .AddEventStore();

        await RegisterChannels(factory);

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

        builder.Services.AddSwaggerGen();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

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


        async Task RegisterChannels(ConnectionFactory factory)
        {
            var connection = await factory.CreateConnectionAsync();

            var createdTransactionConsumerChannel = new CreatedTransactionConsumerChannel(connection);

            await createdTransactionConsumerChannel.InitializeChannelAsync();

            builder.Services.AddSingleton<ICreatedTransactionConsumerChannel>(createdTransactionConsumerChannel);

            var createdConsolidationPublisherChannel = new CreatedConsolidationPublisherChannel(connection);

            await createdConsolidationPublisherChannel.InitializeChannelAsync();

            builder.Services.AddSingleton<ICreatedConsolidationPublisherChannel>(createdConsolidationPublisherChannel);

            var rabbitMqQueueInitializerChannel = new RabbitMqQueueInitializerChannel(connection);

            await rabbitMqQueueInitializerChannel.InitializeChannelAsync();

            builder.Services.AddSingleton<IRabbitMqQueueInitializerChannel>(rabbitMqQueueInitializerChannel);
        }
    }
}