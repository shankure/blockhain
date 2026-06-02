using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using SecureTrace.API.Data;
using SecureTrace.API.DTOs;
using SecureTrace.API.Services;

namespace SecureTrace.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AuditController : ControllerBase
{
    private readonly IVerificationService _verificationService;
    private readonly MongoDbContext       _mongo;

    public AuditController(IVerificationService verificationService, MongoDbContext mongo)
    {
        _verificationService = verificationService;
        _mongo               = mongo;
    }

    /// Verifies the entire cryptographic chain in MongoDB.
    /// Re-computes every hash and checks every link.
    /// All three roles can run this — it is the Auditor's primary tool.
    [HttpGet("verify")]
    [Authorize(Roles = "Admin,User,Auditor")]
    public async Task<IActionResult> Verify()
    {
        var result = await _verificationService.VerifyChainAsync();

        // Return 200 regardless — the IsValid field tells the client the truth.
        // We don't use 4xx/5xx here because a tampered chain is a valid
        // business response, not an HTTP error.
        return Ok(result);
    }

    /// Returns all audit blocks ordered by BlockIndex ascending.
    /// Used by the frontend to render the ledger visualization.
    [HttpGet("blocks")]
    [Authorize(Roles = "Admin,User,Auditor")]
    public async Task<IActionResult> GetBlocks()
    {
        var blocks = await _mongo.AuditBlocks
            .Find(Builders<SecureTrace.API.Models.AuditBlock>.Filter.Empty)
            .SortBy(b => b.BlockIndex)
            .ToListAsync();

        var response = blocks.Select(b => new AuditBlockResponse(
            BlockIndex:       b.BlockIndex,
            PreviousHash:     b.PreviousHash,
            CurrentHash:      b.CurrentHash,
            EvidenceId:       b.EvidenceId,
            ActionType:       b.ActionType,
            EvidenceSnapshot: b.EvidenceSnapshot,
            ActorEmail:       b.ActorEmail,
            Timestamp:        b.Timestamp.ToString("O")
        ));

        return Ok(response);
    }
}
