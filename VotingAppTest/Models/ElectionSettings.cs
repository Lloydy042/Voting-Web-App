using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace VotingAppTest.Models
{
    public class ElectionSettings
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }
        public bool ShowResults { get; set; } = false;
        public string Course { get; set; }   // e.g. "BSIT"
        public string Section { get; set; }  // e.g. "A"
        public int YearLevel { get; set; }   // e.g. 1, 2, 3, 4

        public string Category { get; set; } // e.g. "StudentCouncil"
    }
}
