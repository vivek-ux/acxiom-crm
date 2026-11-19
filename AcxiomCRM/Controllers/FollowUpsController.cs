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
public class FollowUpsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _auditService;

    public FollowUpsController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IAuditService auditService)
    {
        _context = context;
        _userManager = userManager;
        _auditService = auditService;
    }

    public async Task<IActionResult> Index(string? status, string? type)
    {
        var user = await _userManager.GetUserAsync(User);
        var isSales = User.IsInRole("SalesExecutive");

        IQueryable<FollowUp> query = _context.FollowUps
            .Include(f => f.Customer)
            .Include(f => f.Lead);

        if (isSales && user != null)
        {
            query = query.Where(f => f.AssignedTo == user.Id);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(f => f.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            query = query.Where(f => f.FollowUpType == type);
        }

        var followUps = await query.OrderBy(f => f.FollowUpDate).ToListAsync();
        ViewBag.Status = status;
        ViewBag.Type = type;
        return View(followUps);
    }

    public async Task<IActionResult> Create(int? customerId = null, int? leadId = null)
    {
        await PopulateDropDowns(customerId, leadId);
        return View(new FollowUp
        {
            FollowUpDate = DateTime.UtcNow.AddDays(1),
            CustomerId = customerId,
            LeadId = leadId
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(FollowUp followUp)
    {
        // Business Validation (Section 5.3 & 17.7): Date cannot be in past for planned follow-up
        if (followUp.Status == "Planned" && followUp.FollowUpDate.Date < DateTime.UtcNow.Date)
        {
            ModelState.AddModelError("FollowUpDate", "Follow-up date cannot be earlier than today.");
        }

        if (ModelState.IsValid)
        {
            var user = await _userManager.GetUserAsync(User);
            followUp.CreatedDate = DateTime.UtcNow;

            if (User.IsInRole("SalesExecutive") && user != null)
            {
                followUp.AssignedTo = user.Id;
            }

            _context.Add(followUp);
            await _context.SaveChangesAsync();

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            await _auditService.LogAsync(
                user?.Email ?? "User",
                "Create",
                "FollowUp",
                followUp.FollowUpId.ToString(),
                null,
                followUp.Subject,
                $"Follow-up scheduled for {followUp.FollowUpDate:g}. Type: {followUp.FollowUpType}",
                ip);

            TempData["SuccessMessage"] = "Follow-up scheduled successfully.";
            return RedirectToAction(nameof(Index));
        }

        await PopulateDropDowns(followUp.CustomerId, followUp.LeadId, followUp.AssignedTo);
        return View(followUp);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(int id, string? remarks)
    {
        var followUp = await _context.FollowUps.FindAsync(id);
        if (followUp == null) return NotFound();

        followUp.Status = "Completed";
        if (!string.IsNullOrWhiteSpace(remarks))
        {
            followUp.Remarks = (followUp.Remarks != null ? followUp.Remarks + "\n" : "") + $"[Completed on {DateTime.UtcNow:g}]: " + remarks;
        }

        _context.Update(followUp);
        await _context.SaveChangesAsync();

        var user = await _userManager.GetUserAsync(User);
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _auditService.LogAsync(
            user?.Email ?? "User",
            "Update",
            "FollowUp",
            id.ToString(),
            "Planned",
            "Completed",
            $"Follow-up '{followUp.Subject}' marked Completed",
            ip);

        TempData["SuccessMessage"] = "Follow-up marked as Completed.";
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateDropDowns(int? customerId = null, int? leadId = null, string? assignedTo = null)
    {
        ViewBag.Customers = new SelectList(await _context.Customers.OrderBy(c => c.CustomerName).ToListAsync(), "CustomerId", "CustomerName", customerId);
        ViewBag.Leads = new SelectList(await _context.Leads.Where(l => l.Status != "Converted").OrderBy(l => l.LeadName).ToListAsync(), "LeadId", "LeadName", leadId);
        var salesUsers = await _userManager.GetUsersInRoleAsync("SalesExecutive");
        ViewBag.SalesUsers = new SelectList(salesUsers, "Id", "FullName", assignedTo);
    }
}
