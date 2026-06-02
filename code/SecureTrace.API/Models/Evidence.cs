namespace SecureTrace.API.Models;

public class Evidence
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// Type values: "Photo", "Video", "Document", "Other"
    public string EvidenceType { get; set; } = "Other";

    /// Path or URL to the stored file (e.g. Azure Blob, local path)
    public string FileReference { get; set; } = string.Empty;

    public DateTime CollectedAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Foreign key → which Case this evidence belongs to
    public int CaseId { get; set; }
    public Case? Case { get; set; }

    // Foreign key → who uploaded it
    public int UploadedByUserId { get; set; }
    public User? UploadedBy { get; set; }
}
