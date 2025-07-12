using MongoDB.Bson;

namespace AccountService.Presentation
{
    public record UserResultDto(string AccountId, string Token);
}
