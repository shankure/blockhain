using MongoDB.Driver;
using SecureTrace.API.Data;
using SecureTrace.API.Models;
using SecureTrace.API.Services.Interfaces;

namespace SecureTrace.API.Services;

/// Responsible for appending new blocks to the MongoDB audit ledger.
/// Every call to AppendBlockAsync creates ONE new document in the
/// audit_blocks collection, chained to the previous block's hash.
public class AuditService : IAuditService
{
    private readonly MongoDbContext      _mongo;
    private readonly ICryptographyService _crypto;

    public AuditService(MongoDbContext mongo, ICryptographyService crypto)
    {
        _mongo  = mongo;
        _crypto = crypto;
    }

    public async Task AppendBlockAsync(Evidence evidence, string actionType, string actorEmail)
    {
        // Step 1: Find the last block in the chain
        // We sort by BlockIndex descending and take the first result.
        // This gives us the most recent block so we can read its CurrentHash.

        var lastBlock = await _mongo.AuditBlocks
            .Find(Builders<AuditBlock>.Filter.Empty)        // no filter = all documents
            .SortByDescending(b => b.BlockIndex)            // newest first
            .Limit(1)
            .FirstOrDefaultAsync();                         // null if collection is empty

        // Step 2: Determine the new block's index and previous hash
        
        // If this is the very first block (genesis block), we use:
        //   BlockIndex    = 1
        //   PreviousHash  = "0000000000000000" (conventional genesis marker)
        // Otherwise we increment the last block's index and use its hash.

        var newBlockIndex  = lastBlock is null ? 1 : lastBlock.BlockIndex + 1;
        var previousHash   = lastBlock is null ? "0000000000000000" : lastBlock.CurrentHash;

        // Step 3: Build the snapshot
     
        // The snapshot is a string representation of the evidence data
        // AT THIS MOMENT IN TIME. If someone later edits the evidence
        // description in the SQL database, the snapshot here won't change —
        // so the hash will no longer match when we verify.
        // We include the most important fields that prove integrity.

        var snapshot = BuildSnapshot(evidence);

        // Step 4: Compute the current hash 
       
        // We build the exact payload string and hash it with SHA-256.
        // The timestamp is fixed NOW so it becomes part of the immutable record.

        var timestamp = DateTime.UtcNow;
        var payload   = _crypto.BuildBlockPayload(
            blockIndex:        newBlockIndex,
            timestamp:         timestamp,
            evidenceId:        evidence.Id,
            actionType:        actionType,
            evidenceSnapshot:  snapshot,
            actorEmail:        actorEmail,
            previousHash:      previousHash
        );

        var currentHash = _crypto.ComputeHash(payload);

        // Step 5: Insert the new block into MongoDB
        
        // InsertOneAsync adds ONE document to the audit_blocks collection.
        // MongoDB auto-generates the _id (ObjectId) field.
        // We never update or delete audit blocks — append only.

        var newBlock = new AuditBlock
        {
            BlockIndex        = newBlockIndex,
            PreviousHash      = previousHash,
            CurrentHash       = currentHash,
            EvidenceId        = evidence.Id,
            ActionType        = actionType,
            EvidenceSnapshot  = snapshot,
            ActorEmail        = actorEmail,
            Timestamp         = timestamp
        };

        await _mongo.AuditBlocks.InsertOneAsync(newBlock);
    }

    // Private helpers

    /// Builds a deterministic string snapshot of the evidence record.
    /// All fields that matter for integrity are included.
    private static string BuildSnapshot(Evidence evidence)
    {
        return $"id:{evidence.Id};" +
               $"title:{evidence.Title};" +
               $"description:{evidence.Description};" +
               $"type:{evidence.EvidenceType};" +
               $"fileRef:{evidence.FileReference};" +
               $"caseId:{evidence.CaseId};" +
               $"collectedAt:{evidence.CollectedAt:O}";
    }
}
