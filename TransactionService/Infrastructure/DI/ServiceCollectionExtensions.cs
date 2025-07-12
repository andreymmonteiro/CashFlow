using EventStore.Client;
using MongoDB.Driver;
using RabbitMQ.Client;
using TransactionService.Infrastructure.EventStore;
using TransactionService.Infrastructure.Projections;
using static MongoDB.Driver.WriteConcern;

namespace TransactionService.Infrastructure.DI
{
    public static class ServiceCollectionExtensions
    {

        public static ConnectionFactory AddRabbitMq(this IServiceCollection services)
        {
            var factory = new ConnectionFactory
            {
                HostName = "localhost",
                Port = 5671,
                UserName = "guest",
                Password = "guest",
                Ssl = new SslOption
                {
                    Enabled = true,
                    ServerName = "localhost",
                    Version = System.Security.Authentication.SslProtocols.Tls12,

                    // local and development purpose
                    CertificateValidationCallback = (sender, certificate, chain, errors) => true
                }
            };

            services.AddSingleton(factory);

            services.AddHostedService<RabbitMqQueueInitializer>();


            return factory;
        }

        public static IServiceCollection AddMongoDb(this IServiceCollection services)
        {
            services.AddSingleton<IMongoClient>(sp =>
            {
                var settings = MongoClientSettings.FromConnectionString(
                    "mongodb://root:1234@localhost:27018/?retryWrites=true");
                return new MongoClient(settings);
            });

            services.AddScoped(sp =>
            {
                var client = sp.GetRequiredService<IMongoClient>();
                var db = client.GetDatabase("CashFlowDb");
                return db.GetCollection<TransactionProjection>("transactions");
            });

            return services;
        }

        public static IServiceCollection AddEventStore(this IServiceCollection services)
        {
            services.AddSingleton(sp =>
            {
                var settings = EventStoreClientSettings.Create("esdb://localhost:2113?tls=false");
                return new EventStoreClient(settings);
            });

            services.AddSingleton<IEventStoreWrapper, EventStoreWrapper>();

            return services;
        }
    }
}
