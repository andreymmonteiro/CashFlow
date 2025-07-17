namespace AccountService.Infrastructure.Options;

public class MongoDbOptions
{
    public const string SectionName = "MongoDb";

    public string ConnectionString { get; set; }

    public string DatabaseName { get; set; }

    public string CollectionName { get; set; }
}