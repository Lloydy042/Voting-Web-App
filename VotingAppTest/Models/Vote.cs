using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace VotingAppTest.Models
{
    public class Vote
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }
        public string StudentId { get; set; }  
        public string CandidateId { get; set; } 

        public string CandidateName { get; set; }
        public DateTime Timestamp { get; set; }

        public string Position { get; set; }

        public string Category { get; set; }

    }
}
