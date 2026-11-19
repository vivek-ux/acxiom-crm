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
public class CustomersController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _auditService;

    public CustomersController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IAuditService auditService)
    {
        _context = context;
        _userManager = userManager;
        _auditService = auditService;
    }

    public async Task<IActionResult> Index(string? search, string? status)
    {
        var user = await _userManager.GetUserAsync(User);
        var isSales = User.IsInRole("SalesExecutive");

        IQueryable<Customer> query = _context.Customers;
        if (isSales && user != null)
        {
            query = query.Where(c => c.OwnerId == user.Id || c.CreatedBy == user.UserName);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();
            query = query.Where(c =>
                c.CustomerName.Contains(search) ||
                c.Email.Contains(search) ||
                c.Phone.Contains(search) ||
                (c.CompanyName != null && c.CompanyName.Contains(search)));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(c => c.Status == status);
        }

        var customers = await query.OrderByDescending(c => c.CreatedDate).ToListAsync();
        ViewBag.Search = search;
        ViewBag.Status = status;
        return View(customers);
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        var customer = await _context.Customers
            .Include(c => c.Opportunities)
            .Include(c => c.FollowUps)
            .Include(c => c.Activities)
            .FirstOrDefaultAsync(m => m.CustomerId == id);

        if (customer == null) return NotFound();

        var user = await _userManager.GetUserAsync(User);
        if (User.IsInRole("SalesExecutive") && user != null && customer.OwnerId != user.Id && customer.CreatedBy != user.UserName)
        {
            return Forbid();
        }

        return View(customer);
    }

    public async Task<IActionResult> Create()
    {
        var salesUsers = await _userManager.GetUsersInRoleAsync("SalesExecutive");
        ViewBag.SalesUsers = new SelectList(salesUsers, "Id", "FullName");
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Customer customer)
    {
        ModelState.Remove("CustomerCode");
        // 1. Business Validation: Unique Email & Phone (Section 17.5 & 17.7)
        if (await _context.Customers.AnyAsync(c => c.Email.ToLower() == customer.Email.ToLower()))
        {
            ModelState.AddModelError("Email", "A customer with this email address already exists.");
        }

        if (await _context.Customers.AnyAsync(c => c.Phone == customer.Phone))
        {
            ModelState.AddModelError("Phone", "A customer with this phone number already exists.");
        }

        if (ModelState.IsValid)
        {
            var user = await _userManager.GetUserAsync(User);
            customer.CreatedDate = DateTime.UtcNow;
            customer.CreatedBy = user?.UserName ?? "System";

            if (User.IsInRole("SalesExecutive") && user != null)
            {
                customer.OwnerId = user.Id;
            }

            if (string.IsNullOrWhiteSpace(customer.CustomerCode))
            {
                var nextId = (await _context.Customers.CountAsync()) + 1;
                customer.CustomerCode = $"CUST-{nextId:D4}";
            }

            _context.Add(customer);
            await _context.SaveChangesAsync();

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            await _auditService.LogAsync(
                user?.Email ?? "User",
                "Create",
                "Customer",
                customer.CustomerId.ToString(),
                null,
                customer.CustomerName,
                $"Customer {customer.CustomerCode} created",
                ip);

            TempData["SuccessMessage"] = "Customer created successfully.";
            return RedirectToAction(nameof(Index));
        }

        var salesUsers = await _userManager.GetUsersInRoleAsync("SalesExecutive");
        ViewBag.SalesUsers = new SelectList(salesUsers, "Id", "FullName", customer.OwnerId);
        return View(customer);
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var customer = await _context.Customers.FindAsync(id);
        if (customer == null) return NotFound();

        var user = await _userManager.GetUserAsync(User);
        if (User.IsInRole("SalesExecutive") && user != null && customer.OwnerId != user.Id && customer.CreatedBy != user.UserName)
        {
            return Forbid();
        }

        var salesUsers = await _userManager.GetUsersInRoleAsync("SalesExecutive");
        ViewBag.SalesUsers = new SelectList(salesUsers, "Id", "FullName", customer.OwnerId);
        return View(customer);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Customer customer)
    {
        if (id != customer.CustomerId) return NotFound();

        // Unique email & phone check (excluding current record)
        if (await _context.Customers.AnyAsync(c => c.CustomerId != id && c.Email.ToLower() == customer.Email.ToLower()))
        {
            ModelState.AddModelError("Email", "Another customer with this email address already exists.");
        }

        if (await _context.Customers.AnyAsync(c => c.CustomerId != id && c.Phone == customer.Phone))
        {
            ModelState.AddModelError("Phone", "Another customer with this phone number already exists.");
        }

        if (ModelState.IsValid)
        {
            try
            {
                customer.ModifiedDate = DateTime.UtcNow;
                _context.Update(customer);
                await _context.SaveChangesAsync();

                var user = await _userManager.GetUserAsync(User);
                var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
                await _auditService.LogAsync(
                    user?.Email ?? "User",
                    "Update",
                    "Customer",
                    customer.CustomerId.ToString(),
                    null,
                    customer.CustomerName,
                    $"Customer {customer.CustomerCode} updated",
                    ip);

                TempData["SuccessMessage"] = "Customer updated successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.Customers.AnyAsync(e => e.CustomerId == id))
                {
                    return NotFound();
                }
                throw;
            }
        }

        var salesUsers = await _userManager.GetUsersInRoleAsync("SalesExecutive");
        ViewBag.SalesUsers = new SelectList(salesUsers, "Id", "FullName", customer.OwnerId);
        return View(customer);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var customer = await _context.Customers.FindAsync(id);
        if (customer == null) return NotFound();

        var user = await _userManager.GetUserAsync(User);
        if (User.IsInRole("SalesExecutive") && user != null && customer.OwnerId != user.Id)
        {
            return Forbid();
        }

        // Soft delete / deactivation or remove
        customer.Status = "Inactive";
        _context.Update(customer);
        await _context.SaveChangesAsync();

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _auditService.LogAsync(
            user?.Email ?? "User",
            "Delete",
            "Customer",
            id.ToString(),
            customer.CustomerName,
            "Inactive",
            $"Customer {customer.CustomerCode} marked Inactive",
            ip);

        TempData["SuccessMessage"] = "Customer deactivated successfully.";
        return RedirectToAction(nameof(Index));
    }
}
