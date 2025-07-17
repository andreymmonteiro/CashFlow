using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace AccountService.Infrastructure.Projections;

public class UserProjection
{
    [BsonElement("_id")]
    public ObjectId Id { get; set; } = ObjectId.GenerateNewId();

    public string AccountId { get; set; } = default!;

    public string Name { get; set; } = default!;

    public string Email { get; set; } = default!;

    public string PasswordHash { get; set; } = default!;
}
