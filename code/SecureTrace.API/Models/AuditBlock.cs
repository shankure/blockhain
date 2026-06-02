using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SecureTrace.API.Models;

/// Represents a single block in the cryptographic audit ledger.
/// Stored in MongoDB. Every evidence creation or update creates one of these.
/// The chain is: Block[N].PreviousHash == Block[N-1].CurrentHash
public class AuditBlock
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    /// Sequential block number (1, 2, 3 ...). Block #1 is the genesis block.
    public int BlockIndex { get; set; }

    /// The SHA-256 hash of the previous block's data. 
    /// For the genesis block (BlockIndex == 1) this is "0000000000000000".
    public string PreviousHash { get; set; } = string.Empty;

    /// The SHA-256 hash computed over: BlockIndex + Timestamp + EvidenceId + ActionType + ActorEmail + PreviousHash
    public string CurrentHash { get; set; } = string.Empty;

    // Payload

    /// The SQL Server EvidenceId this block is recording an action for.
    public int EvidenceId { get; set; }

    /// What happened: "CREATED", "UPDATED", "DELETED"
    public string ActionType { get; set; } = string.Empty;

    /// Snapshot of the evidence description at the time of this action.
    /// Used during verification to re-compute hashes.
    public string EvidenceSnapshot { get; set; } = string.Empty;

    /// Email of the user who performed the action (audit trail identity).
    public string ActorEmail { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
