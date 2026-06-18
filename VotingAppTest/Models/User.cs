using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace VotingAppTest.Models
{
    public class User
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        public string StudentId { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public string Role { get; set; }
        public string VerificationCode { get; set; }


        public bool IsVerified { get; set; } = false;

        public bool HasVoted { get; set; } = false;
        public string? ResetPasswordToken { get; set; }
        public DateTime? ResetPasswordExpiry { get; set; }

        public string Course { get; set; }
        public string Section { get; set; }
        public int YearLevel { get; set; }



    }
}
