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
public class OpportunitiesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _auditService;

    public OpportunitiesController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IAuditService auditService)
    {
        _context = context;
        _userManager = userManager;
        _auditService = auditService;
    }

    public async Task<IActionResult> Index(string? search, string? stage, string? status)
    {
        var user = await _userManager.GetUserAsync(User);
        var isSales = User.IsInRole("SalesExecutive");

        IQueryable<Opportunity> query = _context.Opportunities
            .Include(o => o.Customer)
            .Include(o => o.Lead);

        if (isSales && user != null)
        {
            query = query.Where(o => o.AssignedTo == user.Id);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();
            query = query.Where(o =>
                o.OpportunityName.Contains(search) ||
                (o.Customer != null && o.Customer.CustomerName.Contains(search)));
        }

        if (!string.IsNullOrWhiteSpace(stage))
        {
            query = query.Where(o => o.Stage == stage);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(o => o.Status == status);
        }

        var opportunities = await query.OrderByDescending(o => o.CreatedDate).ToListAsync();
        ViewBag.Search = search;
        ViewBag.Stage = stage;
        ViewBag.Status = status;
        return View(opportunities);
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        var opp = await _context.Opportunities
            .Include(o => o.Customer)
            .Include(o => o.Lead)
            .FirstOrDefaultAsync(m => m.OpportunityId == id);

        if (opp == null) return NotFound();

        var user = await _userManager.GetUserAsync(User);
        if (User.IsInRole("SalesExecutive") && user != null && opp.AssignedTo != user.Id)
        {
            return Forbid();
        }

        return View(opp);
    }

    public async Task<IActionResult> Create()
    {
        await PopulateDropDowns();
        return View(new Opportunity { ExpectedCloseDate = DateTime.UtcNow.AddDays(30) });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Opportunity opportunity)
    {
        // 1. Mandatory Business Validations (Section 5.3, 17.7, 17.19)
        if (opportunity.Amount <= 0)
        {
            ModelState.AddModelError("Amount", "Opportunity Amount must be greater than 0.");
        }

        if (opportunity.Probability < 0 || opportunity.Probability > 100)
        {
            ModelState.AddModelError("Probability", "Probability must be between 0 and 100.");
        }

        if (opportunity.ExpectedCloseDate.Date < DateTime.UtcNow.Date && opportunity.Stage != "Won" && opportunity.Stage != "Lost")
        {
            ModelState.AddModelError("ExpectedCloseDate", "Expected Close Date cannot be in the past.");
        }

        if (ModelState.IsValid)
        {
            var user = await _userManager.GetUserAsync(User);
            opportunity.CreatedDate = DateTime.UtcNow;

            if (User.IsInRole("SalesExecutive") && user != null)
            {
                opportunity.AssignedTo = user.Id;
            }

            // Sync status with stage
            if (opportunity.Stage == "Won")
            {
                opportunity.Status = "Won";
                opportunity.Probability = 100;
            }
            else if (opportunity.Stage == "Lost")
            {
                opportunity.Status = "Lost";
                opportunity.Probability = 0;
            }
            else
            {
                opportunity.Status = "Open";
            }

            _context.Add(opportunity);
            await _context.SaveChangesAsync();

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            await _auditService.LogAsync(
                user?.Email ?? "User",
                "Create",
                "Opportunity",
                opportunity.OpportunityId.ToString(),
                null,
                opportunity.OpportunityName,
                $"Opportunity {opportunity.OpportunityName} created. Amount: ${opportunity.Amount:N2}, Stage: {opportunity.Stage}",
                ip);

            TempData["SuccessMessage"] = "Opportunity created successfully.";
            return RedirectToAction(nameof(Index));
        }

        await PopulateDropDowns(opportunity.CustomerId, opportunity.LeadId, opportunity.AssignedTo);
        return View(opportunity);
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var opp = await _context.Opportunities.FindAsync(id);
        if (opp == null) return NotFound();

        var user = await _userManager.GetUserAsync(User);
        if (User.IsInRole("SalesExecutive") && user != null && opp.AssignedTo != user.Id)
        {
            return Forbid();
        }

        await PopulateDropDowns(opp.CustomerId, opp.LeadId, opp.AssignedTo);
        return View(opp);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Opportunity opportunity)
    {
        if (id != opportunity.OpportunityId) return NotFound();

        // 1. Mandatory Business Validations (Section 5.3, 17.7, 17.19)
        if (opportunity.Amount <= 0)
        {
            ModelState.AddModelError("Amount", "Opportunity Amount must be greater than 0.");
        }

        if (opportunity.Probability < 0 || opportunity.Probability > 100)
        {
            ModelState.AddModelError("Probability", "Probability must be between 0 and 100.");
        }

        if (opportunity.ExpectedCloseDate.Date < DateTime.UtcNow.Date && opportunity.Stage != "Won" && opportunity.Stage != "Lost")
        {
            ModelState.AddModelError("ExpectedCloseDate", "Expected Close Date cannot be in the past.");
        }

        if (ModelState.IsValid)
        {
            try
            {
                if (opportunity.Stage == "Won")
                {
                    opportunity.Status = "Won";
                    opportunity.Probability = 100;
                }
                else if (opportunity.Stage == "Lost")
                {
                    opportunity.Status = "Lost";
                    opportunity.Probability = 0;
                }
                else
                {
                    opportunity.Status = "Open";
                }

                _context.Update(opportunity);
                await _context.SaveChangesAsync();

                var user = await _userManager.GetUserAsync(User);
                var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
                await _auditService.LogAsync(
                    user?.Email ?? "User",
                    "Update",
                    "Opportunity",
                    opportunity.OpportunityId.ToString(),
                    null,
                    opportunity.OpportunityName,
                    $"Opportunity {opportunity.OpportunityName} updated. Amount: ${opportunity.Amount:N2}, Stage: {opportunity.Stage}",
                    ip);

                TempData["SuccessMessage"] = "Opportunity updated successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.Opportunities.AnyAsync(e => e.OpportunityId == id))
                {
                    return NotFound();
                }
                throw;
            }
        }

        await PopulateDropDowns(opportunity.CustomerId, opportunity.LeadId, opportunity.AssignedTo);
        return View(opportunity);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var opp = await _context.Opportunities.FindAsync(id);
        if (opp == null) return NotFound();

        var user = await _userManager.GetUserAsync(User);
        if (User.IsInRole("SalesExecutive") && user != null && opp.AssignedTo != user.Id)
        {
            return Forbid();
        }

        _context.Opportunities.Remove(opp);
        await _context.SaveChangesAsync();

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _auditService.LogAsync(
            user?.Email ?? "User",
            "Delete",
            "Opportunity",
            id.ToString(),
            opp.OpportunityName,
            null,
            $"Opportunity {opp.OpportunityName} deleted",
            ip);

        TempData["SuccessMessage"] = "Opportunity deleted successfully.";
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
