namespace SecureTrace.API.Models;

public class Case
{
    public int Id { get; set; }
    public string CaseNumber { get; set; } = string.Empty;  // e.g. "CASE-2025-001"
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// Status values: "Open", "Closed", "Archived"
    public string Status { get; set; } = "Open";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }

    // Foreign key → the Admin/Investigator who owns the case
    public int CreatedByUserId { get; set; }
    public User? CreatedBy { get; set; }

    // Navigation
    public ICollection<Evidence> Evidences { get; set; } = new List<Evidence>();
}
