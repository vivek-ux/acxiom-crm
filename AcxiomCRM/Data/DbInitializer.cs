using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Models;

namespace AcxiomCRM.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        // Ensure SQLite database & schema are created
        await context.Database.EnsureCreatedAsync();

        // 1. Seed Roles
        string[] roles = { "Admin", "Manager", "SalesExecutive" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        // 2. Seed Default Users
        var usersToSeed = new (string Email, string Name, string Role, string Password)[]
        {
            ("admin@acxiom.com", "System Administrator", "Admin", "Admin@123"),
            ("manager@acxiom.com", "Sales Manager", "Manager", "Manager@123"),
            ("sales@acxiom.com", "Sarah Jenkins", "SalesExecutive", "Sales@123"),
            ("sales2@acxiom.com", "David Miller", "SalesExecutive", "Sales@123")
        };

        foreach (var (email, name, role, password) in usersToSeed)
        {
            var existingUser = await userManager.FindByEmailAsync(email);
            if (existingUser == null)
            {
                var user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    FullName = name,
                    RoleName = role,
                    CreatedDate = DateTime.UtcNow
                };

                var result = await userManager.CreateAsync(user, password);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(user, role);
                }
            }
        }

        // 3. Seed Customers if empty
        if (!context.Customers.Any())
        {
            var salesUser = await userManager.FindByEmailAsync("sales@acxiom.com");
            var sales2User = await userManager.FindByEmailAsync("sales2@acxiom.com");

            var c1 = new Customer
            {
                CustomerCode = "CUST-001",
                CustomerName = "Apex Financial Technologies",
                Email = "procurement@apexfintech.com",
                Phone = "9876543210",
                CompanyName = "Apex Group",
                Address = "Tech Hub, Sector 62",
                City = "Noida",
                State = "Uttar Pradesh",
                Status = "Active",
                CreatedDate = DateTime.UtcNow.AddDays(-30),
                CreatedBy = "Admin",
                OwnerId = salesUser?.Id
            };

            var c2 = new Customer
            {
                CustomerCode = "CUST-002",
                CustomerName = "BlueHorizon Retail Solutions",
                Email = "contact@bluehorizon.io",
                Phone = "9823456789",
                CompanyName = "BlueHorizon Inc",
                Address = "MG Road, Indiranagar",
                City = "Bengaluru",
                State = "Karnataka",
                Status = "Active",
                CreatedDate = DateTime.UtcNow.AddDays(-20),
                CreatedBy = "Admin",
                OwnerId = salesUser?.Id
            };

            var c3 = new Customer
            {
                CustomerCode = "CUST-003",
                CustomerName = "Quantum Logistics Corp",
                Email = "supply@quantumlogistics.in",
                Phone = "9988776655",
                CompanyName = "Quantum Global",
                Address = "Bandra Kurla Complex",
                City = "Mumbai",
                State = "Maharashtra",
                Status = "Active",
                CreatedDate = DateTime.UtcNow.AddDays(-15),
                CreatedBy = "Admin",
                OwnerId = sales2User?.Id
            };

            context.Customers.AddRange(c1, c2, c3);
            await context.SaveChangesAsync();

            // 4. Seed Leads
            var l1 = new Lead
            {
                LeadCode = "LEAD-101",
                LeadName = "Global Telecom Networks",
                Email = "info@globaltelecom.com",
                Phone = "9712345678",
                CompanyName = "Global Telecom",
                Source = "Website",
                Status = "Qualified",
                ExpectedValue = 450000m,
                Priority = "High",
                Notes = "Enterprise ERP migration inquiry. High purchase intent.",
                CreatedDate = DateTime.UtcNow.AddDays(-10),
                AssignedTo = salesUser?.Id
            };

            var l2 = new Lead
            {
                LeadCode = "LEAD-102",
                LeadName = "Zion Health Systems",
                Email = "admin@zionhealth.org",
                Phone = "9654321098",
                CompanyName = "Zion Hospitals",
                Source = "Referral",
                Status = "Contacted",
                ExpectedValue = 280000m,
                Priority = "Medium",
                Notes = "Demo scheduled for telemedicine CRM module.",
                CreatedDate = DateTime.UtcNow.AddDays(-7),
                AssignedTo = salesUser?.Id
            };

            var l3 = new Lead
            {
                LeadCode = "LEAD-103",
                LeadName = "Starlight Media Agency",
                Email = "marketing@starlightmedia.com",
                Phone = "9123456780",
                CompanyName = "Starlight Ltd",
                Source = "Social",
                Status = "New",
                ExpectedValue = 150000m,
                Priority = "Low",
                Notes = "Downloaded product brochure.",
                CreatedDate = DateTime.UtcNow.AddDays(-2),
                AssignedTo = sales2User?.Id
            };

            context.Leads.AddRange(l1, l2, l3);
            await context.SaveChangesAsync();

            // 5. Seed Opportunities
            var op1 = new Opportunity
            {
                OpportunityName = "Apex Cloud Migration Contract",
                CustomerId = c1.CustomerId,
                Amount = 850000m,
                Stage = "Negotiation",
                Probability = 80,
                ExpectedCloseDate = DateTime.UtcNow.AddDays(25),
                Status = "Open",
                CreatedDate = DateTime.UtcNow.AddDays(-15),
                AssignedTo = salesUser?.Id,
                Notes = "Legal contract under review by procurement."
            };

            var op2 = new Opportunity
            {
                OpportunityName = "BlueHorizon POS Integration",
                CustomerId = c2.CustomerId,
                Amount = 320000m,
                Stage = "Proposal",
                Probability = 50,
                ExpectedCloseDate = DateTime.UtcNow.AddDays(40),
                Status = "Open",
                CreatedDate = DateTime.UtcNow.AddDays(-10),
                AssignedTo = salesUser?.Id,
                Notes = "Technical architecture proposal submitted."
            };

            var op3 = new Opportunity
            {
                OpportunityName = "Quantum Fleet Analytics Module",
                CustomerId = c3.CustomerId,
                Amount = 600000m,
                Stage = "Won",
                Probability = 100,
                ExpectedCloseDate = DateTime.UtcNow.AddDays(-5),
                Status = "Won",
                CreatedDate = DateTime.UtcNow.AddDays(-28),
                AssignedTo = sales2User?.Id,
                Notes = "Master Services Agreement signed."
            };

            var op4 = new Opportunity
            {
                OpportunityName = "Apex Security Audit Add-On",
                CustomerId = c1.CustomerId,
                Amount = 180000m,
                Stage = "Qualification",
                Probability = 25,
                ExpectedCloseDate = DateTime.UtcNow.AddDays(60),
                Status = "Open",
                CreatedDate = DateTime.UtcNow.AddDays(-4),
                AssignedTo = salesUser?.Id,
                Notes = "Initial scoping call."
            };

            context.Opportunities.AddRange(op1, op2, op3, op4);
            await context.SaveChangesAsync();

            // 6. Seed Follow-ups
            var f1 = new FollowUp
            {
                CustomerId = c1.CustomerId,
                FollowUpDate = DateTime.UtcNow.AddDays(2),
                FollowUpType = "Meeting",
                Subject = "Commercial Price Negotiation Call",
                Remarks = "Reviewing payment milestone terms with finance lead.",
                Status = "Planned",
                AssignedTo = salesUser?.Id,
                CreatedDate = DateTime.UtcNow.AddDays(-2)
            };

            var f2 = new FollowUp
            {
                LeadId = l1.LeadId,
                FollowUpDate = DateTime.UtcNow.AddDays(1),
                FollowUpType = "Call",
                Subject = "Technical Architecture Walkthrough",
                Remarks = "Answering CTO questions on multi-tenant security.",
                Status = "Planned",
                AssignedTo = salesUser?.Id,
                CreatedDate = DateTime.UtcNow.AddDays(-1)
            };

            context.FollowUps.AddRange(f1, f2);

            // 7. Seed Activities
            var a1 = new Activity
            {
                ActivityType = "Call",
                Subject = "Introductory Discovery Call",
                Description = "Discussed business scale and API integration needs.",
                ActivityDate = DateTime.UtcNow.AddDays(-12),
                CustomerId = c1.CustomerId,
                AssignedTo = salesUser?.Id,
                Status = "Completed"
            };

            var a2 = new Activity
            {
                ActivityType = "Meeting",
                Subject = "Executive Boardroom Demo",
                Description = "Presented live demo of CRM and ERP connectivity.",
                ActivityDate = DateTime.UtcNow.AddDays(-8),
                CustomerId = c2.CustomerId,
                AssignedTo = salesUser?.Id,
                Status = "Completed"
            };

            context.Activities.AddRange(a1, a2);

            // 8. Seed Audit Logs
            var audit1 = new AuditLog
            {
                UserId = "admin@acxiom.com",
                Action = "Login",
                EntityName = "Auth",
                Details = "Administrator logged in successfully.",
                IpAddress = "127.0.0.1",
                CreatedDate = DateTime.UtcNow.AddDays(-1)
            };

            var audit2 = new AuditLog
            {
                UserId = "admin@acxiom.com",
                Action = "Create",
                EntityName = "Customer",
                RecordId = "CUST-001",
                NewValue = "Apex Financial Technologies",
                Details = "Customer record created.",
                IpAddress = "127.0.0.1",
                CreatedDate = DateTime.UtcNow.AddDays(-30)
            };

            context.AuditLogs.AddRange(audit1, audit2);
            await context.SaveChangesAsync();
        }
    }
}
