using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureTrace.API.DTOs;
using SecureTrace.API.Models;
using SecureTrace.API.Repositories.Interfaces;
using SecureTrace.API.Services.Interfaces;
using System.Security.Claims;

namespace SecureTrace.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class EvidenceController : ControllerBase
{
    private readonly IEvidenceRepository _evidenceRepo;
    private readonly ICaseRepository     _caseRepo;
    private readonly IAuditService       _auditService;

    public EvidenceController(
        IEvidenceRepository evidenceRepo,
        ICaseRepository     caseRepo,
        IAuditService       auditService)
    {
        _evidenceRepo = evidenceRepo;
        _caseRepo     = caseRepo;
        _auditService = auditService;
    }

    //  GET /api/evidence
    [HttpGet]
    [Authorize(Roles = "Admin,User,Auditor")]
    public async Task<IActionResult> GetAll()
    {
        var evidences = await _evidenceRepo.GetAllAsync();
        return Ok(evidences.Select(ToResponse));
    }

    // GET /api/evidence/case/{caseId}
    [HttpGet("case/{caseId:int}")]
    [Authorize(Roles = "Admin,User,Auditor")]
    public async Task<IActionResult> GetByCase(int caseId)
    {
        if (!await _caseRepo.ExistsAsync(caseId))
            return NotFound(new { message = $"Case {caseId} not found." });

        var evidences = await _evidenceRepo.GetByCaseIdAsync(caseId);
        return Ok(evidences.Select(ToResponse));
    }

    // GET /api/evidence/{id}
    [HttpGet("{id:int}")]
    [Authorize(Roles = "Admin,User,Auditor")]
    public async Task<IActionResult> GetById(int id)
    {
        var evidence = await _evidenceRepo.GetByIdAsync(id);
        if (evidence is null) return NotFound(new { message = $"Evidence {id} not found." });
        return Ok(ToResponse(evidence));
    }

    // POST /api/evidence
    [HttpPost]
    [Authorize(Roles = "Admin,User")]
    public async Task<IActionResult> Create([FromBody] CreateEvidenceRequest request)
    {
        var userId = GetCurrentUserId();
        var actorEmail = GetCurrentUserEmail();

        if (userId is null)
            return Unauthorized(new { message = "User identity not found in token." });

        if (!await _caseRepo.ExistsAsync(request.CaseId))
            return NotFound(new { message = $"Case {request.CaseId} not found." });

        var evidence = new Evidence
        {
            Title = request.Title,
            Description = request.Description,
            EvidenceType = request.EvidenceType,
            FileReference = request.FileReference,
            CollectedAt = request.CollectedAt,
            CaseId = request.CaseId,
            UploadedByUserId = userId.Value
        };

        var created = await _evidenceRepo.CreateAsync(evidence);

        await _auditService.AppendBlockAsync(created, "CREATED", actorEmail ?? "unknown");

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, ToResponse(created));
    }

    // PUT /api/evidence/{id}
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin,User")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateEvidenceRequest request)
    {
        var actorEmail = GetCurrentUserEmail();

        var updated = await _evidenceRepo.UpdateAsync(id, new Evidence
        {
            Title         = request.Title,
            Description   = request.Description,
            EvidenceType  = request.EvidenceType,
            FileReference = request.FileReference,
            CollectedAt   = request.CollectedAt
        });

        if (updated is null) return NotFound(new { message = $"Evidence {id} not found." });

        // Append audit block for the update
        // Every update creates a new block. This means the ledger records
        // the FULL history of changes, not just the current state.
        await _auditService.AppendBlockAsync(updated, "UPDATED", actorEmail ?? "unknown");

        return Ok(ToResponse(updated));
    }

    // DELETE /api/evidence/{id}
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _evidenceRepo.DeleteAsync(id);
        if (!deleted) return NotFound(new { message = $"Evidence {id} not found." });
        return NoContent();
    }

    // Helpers

    private int? GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? User.FindFirstValue("sub");
        return int.TryParse(sub, out var id) ? id : null;
    }

    private string? GetCurrentUserEmail()
    {
        return User.FindFirstValue(ClaimTypes.Email)
            ?? User.FindFirstValue("email");
    }

    private static EvidenceResponse ToResponse(Evidence e) => new(
        Id:                 e.Id,
        Title:              e.Title,
        Description:        e.Description,
        EvidenceType:       e.EvidenceType,
        FileReference:      e.FileReference,
        CollectedAt:        e.CollectedAt,
        CreatedAt:          e.CreatedAt,
        CaseId:             e.CaseId,
        CaseNumber:         e.Case?.CaseNumber ?? "Unknown",
        UploadedByFullName: e.UploadedBy?.FullName ?? "Unknown"
    );
}
