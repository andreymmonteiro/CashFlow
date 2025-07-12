using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Transaction.Tests.Unit.Common
{
    internal class ReplaceOneResultTest : ReplaceOneResult
    {
        public override bool IsAcknowledged => true;

        public override bool IsModifiedCountAvailable => true;

        public override long MatchedCount => 1;

        public override long ModifiedCount => 1;

        public override BsonValue UpsertedId => default;
    }
}
