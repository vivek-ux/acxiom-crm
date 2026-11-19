using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Models.ViewModels;

namespace AcxiomCRM.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public DashboardController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login", "Account");
        }

        var isSales = User.IsInRole("SalesExecutive");
        var isManager = User.IsInRole("Manager");
        var isAdmin = User.IsInRole("Admin");

        // Base scoped queries
        IQueryable<Customer> customerQuery = _context.Customers;
        IQueryable<Lead> leadQuery = _context.Leads;
        IQueryable<Opportunity> oppQuery = _context.Opportunities;
        IQueryable<FollowUp> followUpQuery = _context.FollowUps;

        if (isSales)
        {
            customerQuery = customerQuery.Where(c => c.OwnerId == user.Id || c.CreatedBy == user.UserName);
            leadQuery = leadQuery.Where(l => l.AssignedTo == user.Id);
            oppQuery = oppQuery.Where(o => o.AssignedTo == user.Id);
            followUpQuery = followUpQuery.Where(f => f.AssignedTo == user.Id);
        }

        var model = new DashboardViewModel
        {
            UserRole = isAdmin ? "Administrator" : isManager ? "Sales Manager" : "Sales Executive",
            UserEmail = user.Email ?? user.UserName ?? "User",
            TotalCustomers = await customerQuery.CountAsync(),
            TotalLeads = await leadQuery.CountAsync(),
            OpenLeads = await leadQuery.CountAsync(l => l.Status != "Lost" && l.Status != "Converted"),
            TotalOpportunities = await oppQuery.CountAsync(),
            OpenOpportunities = await oppQuery.CountAsync(o => o.Status == "Open"),
            WonOpportunities = await oppQuery.CountAsync(o => o.Status == "Won"),
            LostOpportunities = await oppQuery.CountAsync(o => o.Status == "Lost"),
            TotalPipelineValue = await oppQuery.Where(o => o.Status == "Open").SumAsync(o => (decimal?)o.Amount) ?? 0,
            TotalWeightedPipelineValue = await oppQuery.Where(o => o.Status == "Open").SumAsync(o => (decimal?)((o.Amount * o.Probability) / 100m)) ?? 0,
            RecentLeads = await leadQuery.OrderByDescending(l => l.CreatedDate).Take(5).ToListAsync(),
            UpcomingFollowUps = await followUpQuery.Include(f => f.Customer).Include(f => f.Lead).Where(f => f.Status == "Planned").OrderBy(f => f.FollowUpDate).Take(5).ToListAsync(),
            ActiveOpportunities = await oppQuery.Include(o => o.Customer).Where(o => o.Status == "Open").OrderByDescending(o => o.Amount).Take(5).ToListAsync()
        };

        // 1. Chart.js Data: Lead Status Breakdown
        var leadStatuses = new[] { "New", "Contacted", "Qualified", "Converted", "Lost" };
        var leadStatusCounts = new List<int>();
        foreach (var status in leadStatuses)
        {
            leadStatusCounts.Add(await leadQuery.CountAsync(l => l.Status == status));
        }
        model.LeadStatusLabels = JsonSerializer.Serialize(leadStatuses);
        model.LeadStatusCounts = JsonSerializer.Serialize(leadStatusCounts);

        // 2. Chart.js Data: Pipeline Stages
        var stages = new[] { "Qualification", "Proposal", "Negotiation", "Won", "Lost" };
        var stageAmounts = new List<decimal>();
        foreach (var stage in stages)
        {
            var sum = await oppQuery.Where(o => o.Stage == stage).SumAsync(o => (decimal?)o.Amount) ?? 0;
            stageAmounts.Add(sum);
        }
        model.PipelineStageLabels = JsonSerializer.Serialize(stages);
        model.PipelineStageAmounts = JsonSerializer.Serialize(stageAmounts);

        // 3. Chart.js Data: Monthly Sales / Deals (Last 6 Months)
        var months = new List<string>();
        var monthlyValues = new List<decimal>();
        var today = DateTime.UtcNow;

        for (int i = 5; i >= 0; i--)
        {
            var date = today.AddMonths(-i);
            var monthName = date.ToString("MMM yyyy");
            months.Add(monthName);

            var startOfMonth = new DateTime(date.Year, date.Month, 1);
            var endOfMonth = startOfMonth.AddMonths(1);

            var monthSum = await oppQuery
                .Where(o => o.Status == "Won" && o.CreatedDate >= startOfMonth && o.CreatedDate < endOfMonth)
                .SumAsync(o => (decimal?)o.Amount) ?? 0;

            monthlyValues.Add(monthSum);
        }
        model.MonthlySalesLabels = JsonSerializer.Serialize(months);
        model.MonthlySalesValues = JsonSerializer.Serialize(monthlyValues);

        return View(model);
    }
}
