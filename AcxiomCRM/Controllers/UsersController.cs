using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Models;
using AcxiomCRM.Models.ViewModels;
using AcxiomCRM.Services;

namespace AcxiomCRM.Controllers;

[Authorize(Roles = "Admin")]
public class UsersController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IAuditService _auditService;

    public UsersController(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IAuditService auditService)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _auditService = auditService;
    }

    public async Task<IActionResult> Index()
    {
        var users = await _userManager.Users.ToListAsync();
        var model = new List<UserItemViewModel>();

        foreach (var u in users)
        {
            var userRoles = await _userManager.GetRolesAsync(u);
            model.Add(new UserItemViewModel
            {
                Id = u.Id,
                FullName = u.FullName,
                Email = u.Email ?? u.UserName ?? string.Empty,
                Role = userRoles.FirstOrDefault() ?? "None",
                IsLockedOut = await _userManager.IsLockedOutAsync(u),
                AccessFailedCount = u.AccessFailedCount,
                CreatedDate = u.CreatedDate
            });
        }

        return View(model);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new CreateUserViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateUserViewModel model)
    {
        if (ModelState.IsValid)
        {
            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FullName = model.FullName,
                RoleName = model.Role,
                CreatedDate = DateTime.UtcNow,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (result.Succeeded)
            {
                if (await _roleManager.RoleExistsAsync(model.Role))
                {
                    await _userManager.AddToRoleAsync(user, model.Role);
                }

                var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
                await _auditService.LogAsync(
                    User.Identity?.Name ?? "Admin",
                    "Create",
                    "User",
                    user.Id,
                    null,
                    user.Email,
                    $"Created user {user.Email} with role {model.Role}",
                    ip);

                TempData["SuccessMessage"] = $"User {user.Email} created successfully.";
                return RedirectToAction(nameof(Index));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleLockout(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        var isLocked = await _userManager.IsLockedOutAsync(user);
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        if (isLocked)
        {
            await _userManager.SetLockoutEndDateAsync(user, null);
            await _userManager.ResetAccessFailedCountAsync(user);
            await _auditService.LogAsync(User.Identity?.Name ?? "Admin", "Security", "User", user.Id, "Locked", "Active", $"Account {user.Email} unlocked by Administrator", ip);
            TempData["SuccessMessage"] = $"User {user.Email} unlocked.";
        }
        else
        {
            await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(100));
            await _auditService.LogAsync(User.Identity?.Name ?? "Admin", "Security", "User", user.Id, "Active", "Locked", $"Account {user.Email} locked by Administrator", ip);
            TempData["SuccessMessage"] = $"User {user.Email} locked out.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> ResetPassword(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        return View(new ResetUserPasswordViewModel
        {
            UserId = user.Id,
            Email = user.Email ?? string.Empty
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetUserPasswordViewModel model)
    {
        if (ModelState.IsValid)
        {
            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null) return NotFound();

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);

            if (result.Succeeded)
            {
                var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
                await _auditService.LogAsync(
                    User.Identity?.Name ?? "Admin",
                    "Security",
                    "User",
                    user.Id,
                    null,
                    null,
                    $"Password reset for user {user.Email}",
                    ip);

                TempData["SuccessMessage"] = $"Password for {user.Email} has been reset.";
                return RedirectToAction(nameof(Index));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }

        return View(model);
    }
}
