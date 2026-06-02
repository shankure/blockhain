using MongoDB.Driver;
using SecureTrace.API.Data;
using SecureTrace.API.DTOs;
using SecureTrace.API.Models;
using SecureTrace.API.Services.Interfaces;

namespace SecureTrace.API.Services;

/// The chain verification engine.
///
/// How it works:
///   1. Load ALL blocks from MongoDB, ordered by BlockIndex ascending
///   2. For each block, re-compute its hash from scratch using the stored payload fields
///   3. Compare the re-computed hash with the stored CurrentHash
///      → If they differ, the block's data was altered after it was written
///   4. Compare each block's PreviousHash with the actual previous block's CurrentHash
///      → If they differ, a block was inserted, deleted, or reordered
///   5. If ALL checks pass → chain is intact
///      If ANY check fails → chain is compromised, report exactly which block
public class VerificationService : IVerificationService
{
    private readonly MongoDbContext       _mongo;
    private readonly ICryptographyService _crypto;

    public VerificationService(MongoDbContext mongo, ICryptographyService crypto)
    {
        _mongo  = mongo;
        _crypto = crypto;
    }

    public async Task<VerificationResult> VerifyChainAsync()
    {
        // Step 1: Load all blocks ordered by BlockIndex ascending
        //
        // We MUST process them in order — block 1, then 2, then 3, etc.
        // Sorting by BlockIndex ascending guarantees this.
        //
        var allBlocks = await _mongo.AuditBlocks
            .Find(Builders<AuditBlock>.Filter.Empty)
            .SortBy(b => b.BlockIndex)
            .ToListAsync();

        // Step 2: Handle empty ledger 
        if (allBlocks.Count == 0)
        {
            return new VerificationResult(
                IsValid:      true,
                TotalBlocks:  0,
                Message:      "Ledger is empty. No blocks to verify.",
                Details:      new List<BlockVerificationDetail>()
            );
        }

        // Step 3: Verify each block
        var details      = new List<BlockVerificationDetail>();
        var chainIsValid = true;

        for (int i = 0; i < allBlocks.Count; i++)
        {
            var block          = allBlocks[i];
            var blockIsValid   = true;
            var failureReason  = (string?)null;

            // Check A: Re-compute the hash and compare
            // We rebuild the exact same payload string that was used when
            // this block was originally created in AuditService.AppendBlockAsync.
            // If even ONE character in the stored fields was changed,
            // the re-computed hash will be completely different.
            //
            var recomputedPayload = _crypto.BuildBlockPayload(
                blockIndex:       block.BlockIndex,
                timestamp:        block.Timestamp,
                evidenceId:       block.EvidenceId,
                actionType:       block.ActionType,
                evidenceSnapshot: block.EvidenceSnapshot,
                actorEmail:       block.ActorEmail,
                previousHash:     block.PreviousHash
            );

            var recomputedHash = _crypto.ComputeHash(recomputedPayload);

            if (recomputedHash != block.CurrentHash)
            {
                blockIsValid  = false;
                chainIsValid  = false;
                failureReason = $"Hash mismatch — block data was altered. " +
                                $"Expected: {recomputedHash}, " +
                                $"Stored: {block.CurrentHash}";
            }

            // Check B: Verify PreviousHash linkage
            //
            // For the genesis block (index 1), PreviousHash must be the
            // conventional "0000000000000000" marker.
            //
            // For every other block, PreviousHash must exactly match
            // the CurrentHash of the block that came before it.
            //
            if (blockIsValid)  // only check linkage if the block itself is valid
            {
                if (i == 0)
                {
                    // Genesis block check
                    if (block.PreviousHash != "0000000000000000")
                    {
                        blockIsValid  = false;
                        chainIsValid  = false;
                        failureReason = $"Genesis block has invalid PreviousHash: '{block.PreviousHash}'. Expected '0000000000000000'.";
                    }
                }
                else
                {
                    // Chain linkage check
                    var previousBlock = allBlocks[i - 1];
                    if (block.PreviousHash != previousBlock.CurrentHash)
                    {
                        blockIsValid  = false;
                        chainIsValid  = false;
                        failureReason = $"Chain broken — PreviousHash does not match Block {previousBlock.BlockIndex}'s CurrentHash. " +
                                        $"Expected: {previousBlock.CurrentHash}, " +
                                        $"Stored: {block.PreviousHash}";
                    }
                }
            }

            details.Add(new BlockVerificationDetail(
                BlockIndex:     block.BlockIndex,
                EvidenceId:     block.EvidenceId,
                ActionType:     block.ActionType,
                ActorEmail:     block.ActorEmail,
                Timestamp:      block.Timestamp.ToString("O"),
                IsValid:        blockIsValid,
                FailureReason:  failureReason
            ));
        }

        // Step 4: Build the final result
        var message = chainIsValid
            ? $"✅ Chain verified. All {allBlocks.Count} blocks are intact and unmodified."
            : $"🚨 TAMPER DETECTED. {details.Count(d => !d.IsValid)} block(s) failed verification.";

        return new VerificationResult(
            IsValid:     chainIsValid,
            TotalBlocks: allBlocks.Count,
            Message:     message,
            Details:     details
        );
    }
}
