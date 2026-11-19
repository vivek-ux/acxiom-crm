using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Models.DTOs;
using AcxiomCRM.Services;

namespace AcxiomCRM.Controllers.Api;

[ApiController]
[Route("api/customers")]
public class CustomersApiController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public CustomersApiController(ApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<CustomerDto>>> GetCustomers([FromQuery] string? search)
    {
        IQueryable<Customer> query = _context.Customers;
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(c => c.CustomerName.Contains(search) || c.Email.Contains(search));
        }

        var list = await query.Select(c => new CustomerDto
        {
            CustomerId = c.CustomerId,
            CustomerCode = c.CustomerCode,
            CustomerName = c.CustomerName,
            Email = c.Email,
            Phone = c.Phone,
            CompanyName = c.CompanyName,
            Address = c.Address,
            City = c.City,
            State = c.State,
            Status = c.Status,
            CreatedDate = c.CreatedDate,
            OwnerId = c.OwnerId
        }).ToListAsync();

        return Ok(list);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<CustomerDto>> GetCustomer(int id)
    {
        var c = await _context.Customers.FindAsync(id);
        if (c == null) return NotFound(new { message = $"Customer with ID {id} not found." });

        return Ok(new CustomerDto
        {
            CustomerId = c.CustomerId,
            CustomerCode = c.CustomerCode,
            CustomerName = c.CustomerName,
            Email = c.Email,
            Phone = c.Phone,
            CompanyName = c.CompanyName,
            Address = c.Address,
            City = c.City,
            State = c.State,
            Status = c.Status,
            CreatedDate = c.CreatedDate,
            OwnerId = c.OwnerId
        });
    }

    [HttpPost]
    public async Task<ActionResult<CustomerDto>> CreateCustomer([FromBody] CustomerDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        // Duplicate checks (Section 17.5 & 17.7)
        if (await _context.Customers.AnyAsync(c => c.Email.ToLower() == dto.Email.ToLower()))
        {
            return Conflict(new { message = "A customer with this email already exists." });
        }
        if (await _context.Customers.AnyAsync(c => c.Phone == dto.Phone))
        {
            return Conflict(new { message = "A customer with this phone number already exists." });
        }

        var nextId = (await _context.Customers.CountAsync()) + 1;
        var customer = new Customer
        {
            CustomerCode = $"CUST-{nextId:D4}",
            CustomerName = dto.CustomerName,
            Email = dto.Email,
            Phone = dto.Phone,
            CompanyName = dto.CompanyName,
            Address = dto.Address,
            City = dto.City,
            State = dto.State,
            Status = dto.Status ?? "Active",
            CreatedDate = DateTime.UtcNow,
            CreatedBy = User.Identity?.Name ?? "API",
            OwnerId = dto.OwnerId
        };

        _context.Customers.Add(customer);
        await _context.SaveChangesAsync();

        await _auditService.LogAsync(User.Identity?.Name ?? "API", "Create", "Customer", customer.CustomerId.ToString(), null, customer.CustomerName, "Created via REST API");

        dto.CustomerId = customer.CustomerId;
        dto.CustomerCode = customer.CustomerCode;
        dto.CreatedDate = customer.CreatedDate;

        return CreatedAtAction(nameof(GetCustomer), new { id = customer.CustomerId }, dto);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateCustomer(int id, [FromBody] CustomerDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var customer = await _context.Customers.FindAsync(id);
        if (customer == null) return NotFound(new { message = $"Customer with ID {id} not found." });

        if (await _context.Customers.AnyAsync(c => c.CustomerId != id && c.Email.ToLower() == dto.Email.ToLower()))
        {
            return Conflict(new { message = "Another customer with this email already exists." });
        }

        customer.CustomerName = dto.CustomerName;
        customer.Email = dto.Email;
        customer.Phone = dto.Phone;
        customer.CompanyName = dto.CompanyName;
        customer.Address = dto.Address;
        customer.City = dto.City;
        customer.State = dto.State;
        customer.Status = dto.Status;
        customer.ModifiedDate = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        await _auditService.LogAsync(User.Identity?.Name ?? "API", "Update", "Customer", customer.CustomerId.ToString(), null, customer.CustomerName, "Updated via REST API");

        return Ok(new { message = "Customer updated successfully." });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteCustomer(int id)
    {
        var customer = await _context.Customers.FindAsync(id);
        if (customer == null) return NotFound(new { message = $"Customer with ID {id} not found." });

        customer.Status = "Inactive";
        await _context.SaveChangesAsync();

        await _auditService.LogAsync(User.Identity?.Name ?? "API", "Delete", "Customer", id.ToString(), customer.CustomerName, "Inactive", "Deactivated via REST API");

        return Ok(new { message = "Customer deactivated successfully." });
    }
}

[ApiController]
[Route("api/leads")]
public class LeadsApiController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public LeadsApiController(ApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<LeadDto>>> GetLeads()
    {
        var list = await _context.Leads.Select(l => new LeadDto
        {
            LeadId = l.LeadId,
            LeadCode = l.LeadCode,
            LeadName = l.LeadName,
            Email = l.Email,
            Phone = l.Phone,
            CompanyName = l.CompanyName,
            Source = l.Source,
            Status = l.Status,
            ExpectedValue = l.ExpectedValue,
            Priority = l.Priority,
            Notes = l.Notes,
            AssignedTo = l.AssignedTo,
            CreatedDate = l.CreatedDate
        }).ToListAsync();

        return Ok(list);
    }

    [HttpPost]
    public async Task<ActionResult<LeadDto>> CreateLead([FromBody] LeadDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var nextId = (await _context.Leads.CountAsync()) + 1;
        var lead = new Lead
        {
            LeadCode = $"LEAD-{nextId:D4}",
            LeadName = dto.LeadName,
            Email = dto.Email,
            Phone = dto.Phone,
            CompanyName = dto.CompanyName,
            Source = dto.Source ?? "Website",
            Status = dto.Status ?? "New",
            ExpectedValue = dto.ExpectedValue,
            Priority = dto.Priority ?? "Medium",
            Notes = dto.Notes,
            AssignedTo = dto.AssignedTo,
            CreatedDate = DateTime.UtcNow
        };

        _context.Leads.Add(lead);
        await _context.SaveChangesAsync();

        await _auditService.LogAsync(User.Identity?.Name ?? "API", "Create", "Lead", lead.LeadId.ToString(), null, lead.LeadName, "Created via REST API");

        dto.LeadId = lead.LeadId;
        dto.LeadCode = lead.LeadCode;
        dto.CreatedDate = lead.CreatedDate;

        return CreatedAtAction(nameof(GetLeads), new { id = lead.LeadId }, dto);
    }
}

[ApiController]
[Route("api/opportunities")]
public class OpportunitiesApiController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public OpportunitiesApiController(ApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<OpportunityDto>>> GetOpportunities()
    {
        var list = await _context.Opportunities.Select(o => new OpportunityDto
        {
            OpportunityId = o.OpportunityId,
            OpportunityName = o.OpportunityName,
            CustomerId = o.CustomerId,
            LeadId = o.LeadId,
            Amount = o.Amount,
            Stage = o.Stage,
            Probability = o.Probability,
            ExpectedCloseDate = o.ExpectedCloseDate,
            Status = o.Status,
            AssignedTo = o.AssignedTo
        }).ToListAsync();

        return Ok(list);
    }

    [HttpPost]
    public async Task<ActionResult<OpportunityDto>> CreateOpportunity([FromBody] OpportunityDto dto)
    {
        if (dto.Amount <= 0)
        {
            return BadRequest(new { message = "Opportunity Amount must be greater than 0." });
        }
        if (dto.Probability < 0 || dto.Probability > 100)
        {
            return BadRequest(new { message = "Probability must be between 0 and 100." });
        }
        if (dto.ExpectedCloseDate.Date < DateTime.UtcNow.Date)
        {
            return BadRequest(new { message = "Expected Close Date cannot be in the past." });
        }

        var opp = new Opportunity
        {
            OpportunityName = dto.OpportunityName,
            CustomerId = dto.CustomerId,
            LeadId = dto.LeadId,
            Amount = dto.Amount,
            Stage = dto.Stage ?? "Qualification",
            Probability = dto.Probability,
            ExpectedCloseDate = dto.ExpectedCloseDate,
            Status = dto.Status ?? "Open",
            AssignedTo = dto.AssignedTo,
            CreatedDate = DateTime.UtcNow
        };

        _context.Opportunities.Add(opp);
        await _context.SaveChangesAsync();

        await _auditService.LogAsync(User.Identity?.Name ?? "API", "Create", "Opportunity", opp.OpportunityId.ToString(), null, opp.OpportunityName, "Created via REST API");

        dto.OpportunityId = opp.OpportunityId;
        return CreatedAtAction(nameof(GetOpportunities), new { id = opp.OpportunityId }, dto);
    }
}

[ApiController]
[Route("api/followups")]
public class FollowUpsApiController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public FollowUpsApiController(ApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<FollowUpDto>>> GetFollowUps()
    {
        var list = await _context.FollowUps.Select(f => new FollowUpDto
        {
            FollowUpId = f.FollowUpId,
            CustomerId = f.CustomerId,
            LeadId = f.LeadId,
            FollowUpDate = f.FollowUpDate,
            FollowUpType = f.FollowUpType,
            Subject = f.Subject,
            Remarks = f.Remarks,
            Status = f.Status,
            AssignedTo = f.AssignedTo
        }).ToListAsync();

        return Ok(list);
    }

    [HttpPost]
    public async Task<ActionResult<FollowUpDto>> CreateFollowUp([FromBody] FollowUpDto dto)
    {
        if (dto.Status == "Planned" && dto.FollowUpDate.Date < DateTime.UtcNow.Date)
        {
            return BadRequest(new { message = "Follow-up date cannot be earlier than today." });
        }

        var f = new FollowUp
        {
            CustomerId = dto.CustomerId,
            LeadId = dto.LeadId,
            FollowUpDate = dto.FollowUpDate,
            FollowUpType = dto.FollowUpType ?? "Call",
            Subject = dto.Subject,
            Remarks = dto.Remarks,
            Status = dto.Status ?? "Planned",
            AssignedTo = dto.AssignedTo,
            CreatedDate = DateTime.UtcNow
        };

        _context.FollowUps.Add(f);
        await _context.SaveChangesAsync();

        await _auditService.LogAsync(User.Identity?.Name ?? "API", "Create", "FollowUp", f.FollowUpId.ToString(), null, f.Subject, "Created via REST API");

        dto.FollowUpId = f.FollowUpId;
        return CreatedAtAction(nameof(GetFollowUps), new { id = f.FollowUpId }, dto);
    }
}

[ApiController]
[Route("api/reports")]
public class ReportsApiController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ReportsApiController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("pipeline")]
    public async Task<ActionResult<IEnumerable<PipelineReportDto>>> GetPipelineReport()
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

        return Ok(report);
    }
}
