using MongoDB.Driver;
using VotingAppTest.Models;

public class MongoDbContext
{
    private readonly IMongoDatabase _database;

    public MongoDbContext(IConfiguration config)
    {
        var client = new MongoClient(config["MongoDB:ConnectionString"]);
        _database = client.GetDatabase(config["MongoDB:DatabaseName"]);
    }

    public IMongoCollection<User> Users => _database.GetCollection<User>("Users");
    public IMongoCollection<Candidate> Candidates => _database.GetCollection<Candidate>("Candidates");

    public IMongoCollection<Vote> Votes => _database.GetCollection<Vote>("Votes");
    public IMongoCollection<ElectionSettings> Settings => _database.GetCollection<ElectionSettings>("Settings");
}
