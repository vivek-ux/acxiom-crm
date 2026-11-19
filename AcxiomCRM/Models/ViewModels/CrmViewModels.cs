using System.ComponentModel.DataAnnotations;
using AcxiomCRM.Models;

namespace AcxiomCRM.Models.ViewModels;

public class DashboardViewModel
{
    public int TotalCustomers { get; set; }
    public int TotalLeads { get; set; }
    public int OpenLeads { get; set; }
    public int TotalOpportunities { get; set; }
    public int OpenOpportunities { get; set; }
    public int WonOpportunities { get; set; }
    public int LostOpportunities { get; set; }
    public decimal TotalPipelineValue { get; set; }
    public decimal TotalWeightedPipelineValue { get; set; }

    public string UserRole { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;

    public List<Lead> RecentLeads { get; set; } = new();
    public List<FollowUp> UpcomingFollowUps { get; set; } = new();
    public List<Opportunity> ActiveOpportunities { get; set; } = new();

    // Chart.js JSON Data
    public string LeadStatusLabels { get; set; } = "[]";
    public string LeadStatusCounts { get; set; } = "[]";

    public string PipelineStageLabels { get; set; } = "[]";
    public string PipelineStageAmounts { get; set; } = "[]";

    public string MonthlySalesLabels { get; set; } = "[]";
    public string MonthlySalesValues { get; set; } = "[]";
}

public class LeadConvertViewModel
{
    public int LeadId { get; set; }
    public string LeadName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? CompanyName { get; set; }

    public bool CreateOpportunity { get; set; } = true;

    [StringLength(150)]
    public string? OpportunityName { get; set; }

    [Range(0.01, 1000000000, ErrorMessage = "Opportunity Amount must be greater than 0.")]
    public decimal OpportunityAmount { get; set; } = 10000;

    [Range(0, 100, ErrorMessage = "Probability must be between 0 and 100.")]
    public int Probability { get; set; } = 25;

    [DataType(DataType.Date)]
    public DateTime ExpectedCloseDate { get; set; } = DateTime.UtcNow.AddDays(30);
}

public class UserItemViewModel
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsLockedOut { get; set; }
    public int AccessFailedCount { get; set; }
    public DateTime CreatedDate { get; set; }
}

public class CreateUserViewModel
{
    [Required]
    [StringLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "The password must be at least 8 characters long.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = "SalesExecutive"; // Admin, Manager, SalesExecutive
}

public class ResetUserPasswordViewModel
{
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "The password must be at least 8 characters long.")]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = string.Empty;
}

public class LoginViewModel
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Remember me?")]
    public bool RememberMe { get; set; }
}
