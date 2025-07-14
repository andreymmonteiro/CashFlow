using EventStore.Client;
using MongoDB.Driver;
using RabbitMQ.Client;
using TransactionService.Infrastructure.EventStore;
using TransactionService.Infrastructure.Options;
using TransactionService.Infrastructure.Projections;

namespace TransactionService.Infrastructure.DI
{
    public static class ServiceCollectionExtensions
    {

        public static ConnectionFactory AddRabbitMq(this IServiceCollection services, string host)
        {
            var factory = new ConnectionFactory
            {
                HostName = host,
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

        public static IServiceCollection AddMongoDb(this IServiceCollection services, MongoDbOptions mongoDbOptions)
        {
            services.AddSingleton<IMongoClient>(sp =>
            {
                var settings = MongoClientSettings.FromConnectionString(
                    mongoDbOptions.ConnectionString);
                return new MongoClient(settings);
            });

            services.AddScoped(sp =>
            {
                var client = sp.GetRequiredService<IMongoClient>();
                var db = client.GetDatabase(mongoDbOptions.DatabaseName);
                return db.GetCollection<TransactionProjection>(mongoDbOptions.CollectionName);
            });

            return services;
        }

        public static IServiceCollection AddEventStore(this IServiceCollection services, string connectionString)
        {
            services.AddSingleton(sp =>
            {
                var settings = EventStoreClientSettings.Create(connectionString);
                return new EventStoreClient(settings);
            });

            services.AddSingleton<IEventStoreWrapper, EventStoreWrapper>();

            return services;
        }
    }
}
