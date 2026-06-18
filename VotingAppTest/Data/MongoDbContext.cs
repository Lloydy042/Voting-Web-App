using MongoDB.Driver;
using VotingAppTest.Models;

public class MongoDbContext
{
    private readonly IMongoDatabase _database;

    public MongoDbContext(IConfiguration config)
    {
        var connectionString = Environment.GetEnvironmentVariable("MONGODB_URI") 
                               ?? Environment.GetEnvironmentVariable("MONGO_URL") 
                               ?? config["MongoDB:ConnectionString"];
        var databaseName = Environment.GetEnvironmentVariable("MONGODB_DATABASE") 
                           ?? config["MongoDB:DatabaseName"] 
                           ?? "VotingAppDB";

        var client = new MongoClient(connectionString);
        _database = client.GetDatabase(databaseName);
    }

    public IMongoCollection<User> Users => _database.GetCollection<User>("Users");
    public IMongoCollection<Candidate> Candidates => _database.GetCollection<Candidate>("Candidates");

    public IMongoCollection<Vote> Votes => _database.GetCollection<Vote>("Votes");
    public IMongoCollection<ElectionSettings> Settings => _database.GetCollection<ElectionSettings>("Settings");
}
