using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;

namespace AcxiomCRM.Controllers;

[Authorize(Roles = "Admin,Manager")]
public class AuditLogsController : Controller
{
    private readonly ApplicationDbContext _context;

    public AuditLogsController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(string? userSearch, string? module, string? action)
    {
        IQueryable<Models.AuditLog> query = _context.AuditLogs;

        if (!string.IsNullOrWhiteSpace(userSearch))
        {
            query = query.Where(a => a.UserId.Contains(userSearch));
        }

        if (!string.IsNullOrWhiteSpace(module))
        {
            query = query.Where(a => a.EntityName == module);
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            query = query.Where(a => a.Action == action);
        }

        var logs = await query.OrderByDescending(a => a.CreatedDate).Take(200).ToListAsync();
        ViewBag.UserSearch = userSearch;
        ViewBag.Module = module;
        ViewBag.Action = action;
        return View(logs);
    }
}
