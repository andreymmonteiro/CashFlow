using EventStore.Client;
using Microsoft.AspNetCore.Diagnostics;
using RabbitMQ.Client;
using TransactionService.Application.Commands;
using TransactionService.Application.Queries;
using TransactionService.Infrastructure.DI;
using TransactionService.Presentation.Dtos.Request;
using TransactionService.Presentation.Dtos.Response;

namespace TransactionService;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.

        builder.Services.AddLogging();

        var factory = builder.Services
            .AddRabbitMq();

        builder.Services
            .AddMongoDb()
            .AddEventStore();

        if (!builder.Environment.IsEnvironment("Testing"))
        {
            var connection = await factory.CreateConnectionAsync();
            var channel = await connection.CreateChannelAsync();

            // Register the initialized connection/channel into DI
            builder.Services.AddSingleton(connection);

        }

        //AddSwagger(builder.Services, builder.Environment);

        builder.Services.AddScoped<ICommandHandler<CreateTransactionCommand, Guid>, CreateTransactionCommandHandler>();

        builder.Services.AddScoped<IQueryHandler<DailyTransactionRequest, DailyTransactionResponse>, DailyTransactionQueryHandler>();

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

                //if (exception.Stat)

                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                context.Response.ContentType = "application/json";

                var errorResponse = new
                {
                    Message = "An unexpected error occurred. Please try again later."
                };

                await context.Response.WriteAsJsonAsync(errorResponse);
            };
        });

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            //app.UseSwagger();
            //app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();

        app.UseExceptionHandler();

        app.MapPost("/api/transactions", async (
            CreateTransactionCommand command,
            ICommandHandler<CreateTransactionCommand, Guid> handler,
            CancellationToken cancellationToken) =>
        {
            return await handler.HandleAsync(command, cancellationToken);
        })
        .WithName("CreateTransaction")
        .WithDescription("Creates a new transaction and emits an event")
        .WithSummary("Create transaction");

        app.MapPost("/api/account/transactions/query", async (
                DailyTransactionRequest request,
                IQueryHandler<DailyTransactionRequest, DailyTransactionResponse> handler,
                CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(request, cancellationToken);

            return Results.Ok(result);
        })
        .WithName("GetTransactions")
        .WithDescription("")
        .WithSummary("Get transactions by account and date");

        app.MapGet("/administration/events", async (EventStoreClient client, CancellationToken cancellationToken) =>
        {
            var events = client.ReadAllAsync(Direction.Forwards, EventStore.Client.Position.Start, cancellationToken: cancellationToken);

            var streams = new List<string>();

            await foreach (var @event in events)
            {
                var eventType = @event.Event.EventType;

                if (eventType.StartsWith("$") && @event.Event.EventStreamId.StartsWith("transaction-"))
                {
                    Console.WriteLine($"System event: {eventType}");
                    continue;
                }

                streams.Add(@event.Event.EventStreamId);
            }

            return Results.Ok(streams);
        });

        await app.RunAsync();

        static void AddSwagger(IServiceCollection services, IWebHostEnvironment environment)
        {
            if (!environment.IsEnvironment("FunctionalTest") && !environment.IsEnvironment("FunctionalTest"))
            {
                services.AddSwaggerGen();
            }
        }
    }
}