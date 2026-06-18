using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace VotingAppTest.Models
{
    public class Candidate
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }
        public string FullName { get; set; }
        public string Position { get; set; }
        public string Description { get; set; }
        public string Vision { get; set; }
        public string Plans { get; set; }
        public string ImageUrl { get; set; }

        public byte[] ImageData { get; set; }

        public byte[] QrCode { get; set; }

        public string Category { get; set; }

        public string Course { get; set; }
        public string Section { get; set; }
        public int YearLevel { get; set; }


    }
}
