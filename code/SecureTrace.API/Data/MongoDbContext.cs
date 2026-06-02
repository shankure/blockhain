using MongoDB.Driver;
using SecureTrace.API.Models;

namespace SecureTrace.API.Data;

/// Provides access to MongoDB collections.
/// Registered as a singleton in Program.cs.
/// The protected constructor and virtual property allow Moq to mock this
/// class in unit tests without needing a real MongoDB connection.
public class MongoDbContext
{
    private readonly IMongoDatabase? _database;

    /// Parameterless constructor used by Moq for unit testing only.
    protected MongoDbContext() { }

    public MongoDbContext(IConfiguration configuration)
    {
        var connectionString = configuration["MongoDB:ConnectionString"]
            ?? throw new InvalidOperationException("MongoDB:ConnectionString is not configured.");

        var databaseName = configuration["MongoDB:DatabaseName"]
            ?? throw new InvalidOperationException("MongoDB:DatabaseName is not configured.");

        var client = new MongoClient(connectionString);
        _database = client.GetDatabase(databaseName);

        EnsureIndexes();
    }

    /// The cryptographic audit ledger collection.
    /// Virtual so Moq can override it in unit tests.
    public virtual IMongoCollection<AuditBlock> AuditBlocks =>
        _database!.GetCollection<AuditBlock>("audit_blocks");

    private void EnsureIndexes()
    {
        var indexModel = new CreateIndexModel<AuditBlock>(
            Builders<AuditBlock>.IndexKeys.Ascending(b => b.BlockIndex),
            new CreateIndexOptions { Unique = true }
        );
        AuditBlocks.Indexes.CreateOne(indexModel);
    }
}
