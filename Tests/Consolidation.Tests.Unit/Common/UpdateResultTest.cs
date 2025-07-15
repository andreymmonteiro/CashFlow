using MongoDB.Bson;
using MongoDB.Driver;

namespace Consolidation.Tests.Unit.Common
{
    internal class UpdateResultTest : UpdateResult
    {
        public override bool IsAcknowledged => true;

        public override bool IsModifiedCountAvailable => true;

        public override long MatchedCount => 1;

        public override long ModifiedCount => 1;

        public override BsonValue UpsertedId => default;
    }
}
