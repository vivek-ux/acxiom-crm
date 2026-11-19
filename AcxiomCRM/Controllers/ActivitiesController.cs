using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;

namespace AcxiomCRM.Controllers;

[Authorize]
public class ActivitiesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _auditService;

    public ActivitiesController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IAuditService auditService)
    {
        _context = context;
        _userManager = userManager;
        _auditService = auditService;
    }

    public async Task<IActionResult> Index(string? type, string? status)
    {
        var user = await _userManager.GetUserAsync(User);
        var isSales = User.IsInRole("SalesExecutive");

        IQueryable<Activity> query = _context.Activities
            .Include(a => a.Customer)
            .Include(a => a.Lead);

        if (isSales && user != null)
        {
            query = query.Where(a => a.AssignedTo == user.Id);
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            query = query.Where(a => a.ActivityType == type);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(a => a.Status == status);
        }

        var activities = await query.OrderByDescending(a => a.ActivityDate).ToListAsync();
        ViewBag.Type = type;
        ViewBag.Status = status;
        return View(activities);
    }

    public async Task<IActionResult> Create(int? customerId = null, int? leadId = null)
    {
        await PopulateDropDowns(customerId, leadId);
        return View(new Activity { CustomerId = customerId, LeadId = leadId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Activity activity)
    {
        if (ModelState.IsValid)
        {
            var user = await _userManager.GetUserAsync(User);
            if (User.IsInRole("SalesExecutive") && user != null)
            {
                activity.AssignedTo = user.Id;
            }

            _context.Add(activity);
            await _context.SaveChangesAsync();

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            await _auditService.LogAsync(
                user?.Email ?? "User",
                "Create",
                "Activity",
                activity.ActivityId.ToString(),
                null,
                activity.Subject,
                $"Activity {activity.ActivityType} created: {activity.Subject}",
                ip);

            TempData["SuccessMessage"] = "Activity logged successfully.";
            return RedirectToAction(nameof(Index));
        }

        await PopulateDropDowns(activity.CustomerId, activity.LeadId, activity.AssignedTo);
        return View(activity);
    }

    private async Task PopulateDropDowns(int? customerId = null, int? leadId = null, string? assignedTo = null)
    {
        ViewBag.Customers = new SelectList(await _context.Customers.OrderBy(c => c.CustomerName).ToListAsync(), "CustomerId", "CustomerName", customerId);
        ViewBag.Leads = new SelectList(await _context.Leads.Where(l => l.Status != "Converted").OrderBy(l => l.LeadName).ToListAsync(), "LeadId", "LeadName", leadId);
        var salesUsers = await _userManager.GetUsersInRoleAsync("SalesExecutive");
        ViewBag.SalesUsers = new SelectList(salesUsers, "Id", "FullName", assignedTo);
    }
}
