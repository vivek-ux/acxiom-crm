using AcxiomCRM.Data;
using AcxiomCRM.Models;

namespace AcxiomCRM.Services;

public class AuditService : IAuditService
{
    private readonly ApplicationDbContext _context;

    public AuditService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task LogAsync(string userId, string action, string entityName, string? recordId, string? oldValue, string? newValue, string? details = null, string? ipAddress = null)
    {
        try
        {
            var log = new AuditLog
            {
                UserId = string.IsNullOrWhiteSpace(userId) ? "System/Anonymous" : userId,
                Action = action,
                EntityName = entityName,
                RecordId = recordId,
                OldValue = oldValue,
                NewValue = newValue,
                Details = details,
                IpAddress = ipAddress,
                CreatedDate = DateTime.UtcNow
            };

            _context.AuditLogs.Add(log);
            await _context.SaveChangesAsync();
        }
        catch
        {
            // Fail-safe: Audit logging should not crash business operations if a transient DB error occurs
        }
    }
}
