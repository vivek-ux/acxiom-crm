using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Models.DTOs;

namespace AcxiomCRM.Controllers;

[Authorize]
public class ReportsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public ReportsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        return View();
    }

    public async Task<IActionResult> Pipeline()
    {
        var stages = new[] { "Qualification", "Proposal", "Negotiation", "Won", "Lost" };
        var report = new List<PipelineReportDto>();

        foreach (var s in stages)
        {
            var opps = await _context.Opportunities.Where(o => o.Stage == s).ToListAsync();
            report.Add(new PipelineReportDto
            {
                Stage = s,
                Count = opps.Count,
                TotalAmount = opps.Sum(o => o.Amount),
                TotalWeightedAmount = opps.Sum(o => o.WeightedAmount)
            });
        }

        return View(report);
    }

    public async Task<IActionResult> Conversion()
    {
        var totalLeads = await _context.Leads.CountAsync();
        var convertedLeads = await _context.Leads.CountAsync(l => l.Status == "Converted");
        var lostLeads = await _context.Leads.CountAsync(l => l.Status == "Lost");
        var openLeads = totalLeads - convertedLeads - lostLeads;

        ViewBag.TotalLeads = totalLeads;
        ViewBag.ConvertedLeads = convertedLeads;
        ViewBag.LostLeads = lostLeads;
        ViewBag.OpenLeads = openLeads;
        ViewBag.ConversionRate = totalLeads > 0 ? (convertedLeads * 100.0 / totalLeads) : 0;

        return View();
    }
}
