namespace SecureTrace.API.DTOs;

/// Returned by GET /api/audit/verify
public record VerificationResult(
    bool   IsValid,
    int    TotalBlocks,
    string Message,
    List<BlockVerificationDetail> Details
);

/// Per-block result — shows exactly which block failed and why.
public record BlockVerificationDetail(
    int    BlockIndex,
    int    EvidenceId,
    string ActionType,
    string ActorEmail,
    string Timestamp,
    bool   IsValid,
    string? FailureReason   // null if valid
);

/// Returned by GET /api/audit/blocks — lists all blocks for the UI ledger view.
public record AuditBlockResponse(
    int    BlockIndex,
    string PreviousHash,
    string CurrentHash,
    int    EvidenceId,
    string ActionType,
    string EvidenceSnapshot,
    string ActorEmail,
    string Timestamp
);
