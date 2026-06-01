using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureTrace.API.DTOs;
using SecureTrace.API.Models;
using SecureTrace.API.Repositories.Interfaces;
using System.Security.Claims;

namespace SecureTrace.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]  // All endpoints require a valid JWT by default
public class CasesController : ControllerBase
{
    private readonly ICaseRepository _caseRepo;

    public CasesController(ICaseRepository caseRepo)
    {
        _caseRepo = caseRepo;
    }

    // All authenticated roles can view cases
    [HttpGet]
    [Authorize(Roles = "Admin,User,Auditor")]
    public async Task<IActionResult> GetAll()
    {
        var cases = await _caseRepo.GetAllAsync();
        var response = cases.Select(ToResponse);
        return Ok(response);
    }

    [HttpGet("{id:int}")]
    [Authorize(Roles = "Admin,User,Auditor")]
    public async Task<IActionResult> GetById(int id)
    {
        var c = await _caseRepo.GetByIdAsync(id);
        if (c is null) return NotFound(new { message = $"Case {id} not found." });
        return Ok(ToResponse(c));
    }

    // Only Admins can create cases
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CreateCaseRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized(new { message = "User identity not found in token." });

        var caseEntity = new Case
        {
            Title             = request.Title,
            Description       = request.Description,
            Status            = "Open",
            CreatedByUserId   = userId.Value,
            CreatedAt         = DateTime.UtcNow
        };

        var created = await _caseRepo.CreateAsync(caseEntity);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, ToResponse(created));
    }

    // Only Admins can update cases
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateCaseRequest request)
    {
        var validStatuses = new[] { "Open", "Closed", "Archived" };
        if (!validStatuses.Contains(request.Status))
            return BadRequest(new { message = "Status must be Open, Closed, or Archived." });

        var updated = await _caseRepo.UpdateAsync(id, new Case
        {
            Title       = request.Title,
            Description = request.Description,
            Status      = request.Status
        });

        if (updated is null) return NotFound(new { message = $"Case {id} not found." });
        return Ok(ToResponse(updated));
    }

    // Only Admins can delete cases
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _caseRepo.DeleteAsync(id);
        if (!deleted) return NotFound(new { message = $"Case {id} not found." });
        return NoContent();
    }

    //Helpers

    private int? GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? User.FindFirstValue("sub");
        return int.TryParse(sub, out var id) ? id : null;
    }

    private static CaseResponse ToResponse(Case c) => new(
        Id:                c.Id,
        CaseNumber:        c.CaseNumber,
        Title:             c.Title,
        Description:       c.Description,
        Status:            c.Status,
        CreatedAt:         c.CreatedAt,
        CreatedByFullName: c.CreatedBy?.FullName ?? "Unknown"
    );
}
