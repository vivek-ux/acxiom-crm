using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Models.ViewModels;
using AcxiomCRM.Services;

namespace AcxiomCRM.Controllers;

[Authorize]
public class LeadsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _auditService;

    public LeadsController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IAuditService auditService)
    {
        _context = context;
        _userManager = userManager;
        _auditService = auditService;
    }

    public async Task<IActionResult> Index(string? search, string? status, string? priority)
    {
        var user = await _userManager.GetUserAsync(User);
        var isSales = User.IsInRole("SalesExecutive");

        IQueryable<Lead> query = _context.Leads;
        if (isSales && user != null)
        {
            query = query.Where(l => l.AssignedTo == user.Id);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();
            query = query.Where(l =>
                l.LeadName.Contains(search) ||
                l.Email.Contains(search) ||
                l.Phone.Contains(search) ||
                (l.CompanyName != null && l.CompanyName.Contains(search)));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(l => l.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(priority))
        {
            query = query.Where(l => l.Priority == priority);
        }

        var leads = await query.OrderByDescending(l => l.CreatedDate).ToListAsync();
        ViewBag.Search = search;
        ViewBag.Status = status;
        ViewBag.Priority = priority;
        return View(leads);
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        var lead = await _context.Leads
            .Include(l => l.FollowUps)
            .Include(l => l.Activities)
            .FirstOrDefaultAsync(m => m.LeadId == id);

        if (lead == null) return NotFound();

        var user = await _userManager.GetUserAsync(User);
        if (User.IsInRole("SalesExecutive") && user != null && lead.AssignedTo != user.Id)
        {
            return Forbid();
        }

        return View(lead);
    }

    public async Task<IActionResult> Create()
    {
        var salesUsers = await _userManager.GetUsersInRoleAsync("SalesExecutive");
        ViewBag.SalesUsers = new SelectList(salesUsers, "Id", "FullName");
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Lead lead)
    {
        ModelState.Remove("LeadCode");
        if (ModelState.IsValid)
        {
            var user = await _userManager.GetUserAsync(User);
            lead.CreatedDate = DateTime.UtcNow;

            if (User.IsInRole("SalesExecutive") && user != null)
            {
                lead.AssignedTo = user.Id;
            }

            if (string.IsNullOrWhiteSpace(lead.LeadCode))
            {
                var nextId = (await _context.Leads.CountAsync()) + 1;
                lead.LeadCode = $"LEAD-{nextId:D4}";
            }

            _context.Add(lead);
            await _context.SaveChangesAsync();

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            await _auditService.LogAsync(
                user?.Email ?? "User",
                "Create",
                "Lead",
                lead.LeadId.ToString(),
                null,
                lead.LeadName,
                $"Lead {lead.LeadCode} created with status {lead.Status}",
                ip);

            TempData["SuccessMessage"] = "Lead created successfully.";
            return RedirectToAction(nameof(Index));
        }

        var salesUsers = await _userManager.GetUsersInRoleAsync("SalesExecutive");
        ViewBag.SalesUsers = new SelectList(salesUsers, "Id", "FullName", lead.AssignedTo);
        return View(lead);
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var lead = await _context.Leads.FindAsync(id);
        if (lead == null) return NotFound();

        var user = await _userManager.GetUserAsync(User);
        if (User.IsInRole("SalesExecutive") && user != null && lead.AssignedTo != user.Id)
        {
            return Forbid();
        }

        var salesUsers = await _userManager.GetUsersInRoleAsync("SalesExecutive");
        ViewBag.SalesUsers = new SelectList(salesUsers, "Id", "FullName", lead.AssignedTo);
        return View(lead);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Lead lead)
    {
        if (id != lead.LeadId) return NotFound();

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(lead);
                await _context.SaveChangesAsync();

                var user = await _userManager.GetUserAsync(User);
                var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
                await _auditService.LogAsync(
                    user?.Email ?? "User",
                    "Update",
                    "Lead",
                    lead.LeadId.ToString(),
                    null,
                    lead.LeadName,
                    $"Lead {lead.LeadCode} updated. Status: {lead.Status}",
                    ip);

                TempData["SuccessMessage"] = "Lead updated successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.Leads.AnyAsync(e => e.LeadId == id))
                {
                    return NotFound();
                }
                throw;
            }
        }

        var salesUsers = await _userManager.GetUsersInRoleAsync("SalesExecutive");
        ViewBag.SalesUsers = new SelectList(salesUsers, "Id", "FullName", lead.AssignedTo);
        return View(lead);
    }

    // Lead Conversion (Section 4.5, 8, 17.2)
    [HttpGet]
    public async Task<IActionResult> Convert(int? id)
    {
        if (id == null) return NotFound();

        var lead = await _context.Leads.FindAsync(id);
        if (lead == null) return NotFound();

        if (lead.Status == "Converted")
        {
            TempData["ErrorMessage"] = "This lead is already converted.";
            return RedirectToAction(nameof(Index));
        }

        var model = new LeadConvertViewModel
        {
            LeadId = lead.LeadId,
            LeadName = lead.LeadName,
            Email = lead.Email,
            Phone = lead.Phone,
            CompanyName = lead.CompanyName,
            CreateOpportunity = true,
            OpportunityName = $"{lead.CompanyName ?? lead.LeadName} - Deal",
            OpportunityAmount = lead.ExpectedValue > 0 ? lead.ExpectedValue : 25000m,
            Probability = 25,
            ExpectedCloseDate = DateTime.UtcNow.AddDays(30)
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Convert(LeadConvertViewModel model)
    {
        var lead = await _context.Leads.FindAsync(model.LeadId);
        if (lead == null) return NotFound();

        if (lead.Status == "Converted")
        {
            TempData["ErrorMessage"] = "Lead is already converted.";
            return RedirectToAction(nameof(Index));
        }

        // Validate Opportunity rules if enabled
        if (model.CreateOpportunity)
        {
            if (model.OpportunityAmount <= 0)
            {
                ModelState.AddModelError("OpportunityAmount", "Opportunity Amount must be greater than 0.");
            }
            if (model.Probability < 0 || model.Probability > 100)
            {
                ModelState.AddModelError("Probability", "Probability must be between 0 and 100.");
            }
            if (model.ExpectedCloseDate < DateTime.UtcNow.Date)
            {
                ModelState.AddModelError("ExpectedCloseDate", "Expected Close Date cannot be in the past.");
            }
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.GetUserAsync(User);
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        // 1. Create or Find Customer
        var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Email.ToLower() == lead.Email.ToLower());
        if (customer == null)
        {
            var nextCustId = (await _context.Customers.CountAsync()) + 1;
            customer = new Customer
            {
                CustomerCode = $"CUST-{nextCustId:D4}",
                CustomerName = lead.LeadName,
                Email = lead.Email,
                Phone = lead.Phone,
                CompanyName = lead.CompanyName,
                Status = "Active",
                CreatedDate = DateTime.UtcNow,
                CreatedBy = user?.UserName ?? "System",
                OwnerId = lead.AssignedTo ?? user?.Id
            };
            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();
        }

        // 2. Optionally Create Opportunity
        Opportunity? opp = null;
        if (model.CreateOpportunity)
        {
            opp = new Opportunity
            {
                OpportunityName = model.OpportunityName ?? $"{customer.CustomerName} Deal",
                CustomerId = customer.CustomerId,
                LeadId = lead.LeadId,
                Amount = model.OpportunityAmount,
                Stage = "Qualification",
                Probability = model.Probability,
                ExpectedCloseDate = model.ExpectedCloseDate,
                Status = "Open",
                CreatedDate = DateTime.UtcNow,
                AssignedTo = lead.AssignedTo ?? user?.Id,
                Notes = $"Converted from Lead {lead.LeadCode}"
            };
            _context.Opportunities.Add(opp);
            await _context.SaveChangesAsync();
        }

        // 3. Mark Lead as Converted
        lead.Status = "Converted";
        lead.ConvertedCustomerId = customer.CustomerId;
        lead.ConvertedOpportunityId = opp?.OpportunityId;
        _context.Update(lead);
        await _context.SaveChangesAsync();

        // 4. Audit Log
        await _auditService.LogAsync(
            user?.Email ?? "User",
            "Convert",
            "Lead",
            lead.LeadId.ToString(),
            "Qualified",
            "Converted",
            $"Lead {lead.LeadCode} converted into Customer {customer.CustomerCode}" + (opp != null ? $" and Opportunity {opp.OpportunityName}" : ""),
            ip);

        TempData["SuccessMessage"] = $"Lead successfully converted into Customer '{customer.CustomerName}'" + (opp != null ? $" and Opportunity '{opp.OpportunityName}'." : ".");
        return RedirectToAction(nameof(Index));
    }
}
