using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Models;

public class AuditLog
{
    [Key]
    public int AuditLogId { get; set; }

    [Required]
    [StringLength(100)]
    public string UserId { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Action { get; set; } = string.Empty; // Login, FailedLogin, Create, Update, Delete, RoleChange, Security, Convert

    [Required]
    [StringLength(100)]
    public string EntityName { get; set; } = string.Empty; // Customer, Lead, Opportunity, FollowUp, User, Auth

    [StringLength(100)]
    public string? RecordId { get; set; }

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [StringLength(50)]
    public string? IpAddress { get; set; }

    [StringLength(500)]
    public string? Details { get; set; }
}
