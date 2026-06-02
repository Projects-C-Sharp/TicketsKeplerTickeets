using System.Security.Claims;
using TicketsKeplerTickets.Models.DTOs;
using TicketsKeplerTickets.Models.ViewModels;
using TicketsKeplerTickets.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TicketsKeplerTickets.Controllers;

public class AuthController : Controller
{
    private readonly IApiService _api;

    public AuthController(IApiService api) => _api = api;

    // ─── Login ───────────────────────────────────────────────────────────────

    [HttpGet]
    public IActionResult Login(string? returnUrl = null, string? reason = null)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
        if (reason == "session_expired")
            TempData["WarningMessage"] = "Tu sesión expiró. Por favor inicia sesión nuevamente.";
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var result = await _api.LoginAsync(new LoginDto { Email = vm.Email, Password = vm.Password });

        if (result?.Data == null || !result.Success)
        {
            ModelState.AddModelError("", result?.Message ?? "Credenciales incorrectas");
            return View(vm);
        }

        // Check role — only customers allowed
        if (!result.Data.Roles.Contains("Customer"))
        {
            ModelState.AddModelError("", "Solo los clientes pueden acceder a esta plataforma.");
            return View(vm);
        }

        // Sign in with cookie auth
        var claims = new List<Claim>
        {
            new(ClaimTypes.Email,    result.Data.Email),
            new(ClaimTypes.Name,     result.Data.FullName),
            new(ClaimTypes.Role,     "Customer"),
            new("AccessToken",       result.Data.AccessToken),
            new("RefreshToken",      result.Data.RefreshToken),
        };

        var identity  = new ClaimsIdentity(claims, "KeplerCookies");
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync("KeplerCookies", principal, new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc   = DateTimeOffset.UtcNow.AddHours(8)
        });

        // Also store token in session for JS use
        HttpContext.Session.SetString("AccessToken", result.Data.AccessToken);

        if (!string.IsNullOrEmpty(vm.ReturnUrl) && Url.IsLocalUrl(vm.ReturnUrl))
            return Redirect(vm.ReturnUrl);

        return RedirectToAction("Index", "Home");
    }

    // ─── Register ────────────────────────────────────────────────────────────

    [HttpGet]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
        return View(new RegisterViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var result = await _api.RegisterCustomerAsync(new RegisterDto
        {
            FullName = vm.FullName,
            Email    = vm.Email,
            Password = vm.Password
        });

        if (result == null || !result.Success)
        {
            ModelState.AddModelError("", result?.Message ?? "Error al registrar. Intenta de nuevo.");
            return View(vm);
        }

        TempData["SuccessMessage"] = "¡Cuenta creada exitosamente! Inicia sesión para continuar.";
        return RedirectToAction(nameof(Login));
    }

    // ─── Forgot Password ─────────────────────────────────────────────────────

    [HttpGet]
    public IActionResult ForgotPassword() => View(new ForgotPasswordViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);

        await _api.ForgotPasswordAsync(new ForgotPasswordRequest { Email = vm.Email });

        // Always show success to avoid email enumeration
        TempData["SuccessMessage"] = "Si el correo está registrado, recibirás un mensaje de confirmación. Haz clic en el botón del correo para recibir tu nueva contraseña.";
        return RedirectToAction(nameof(ForgotPasswordConfirmation));
    }

    [HttpGet]
    public IActionResult ForgotPasswordConfirmation() => View();

    // ─── Reset Password ──────────────────────────────────────────────────────

    [HttpGet]
    public IActionResult ResetPassword(string email, string token)
    {
        return View(new ResetPasswordViewModel { Email = email, Token = token });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var result = await _api.ResetPasswordAsync(new ResetPasswordRequest
        {
            Email       = vm.Email,
            Token       = vm.Token,
            NewPassword = vm.NewPassword
        });

        if (result == null || !result.Success)
        {
            ModelState.AddModelError("", result?.Message ?? "El enlace expiró o es inválido.");
            return View(vm);
        }

        TempData["SuccessMessage"] = "¡Contraseña actualizada! Ya puedes iniciar sesión.";
        return RedirectToAction(nameof(Login));
    }

    // ─── Logout ──────────────────────────────────────────────────────────────

    [Authorize]
    public async Task<IActionResult> Logout()
    {
        var token = User.FindFirstValue("AccessToken") ?? string.Empty;
        if (!string.IsNullOrEmpty(token))
            await _api.LogoutAsync(token);

        await HttpContext.SignOutAsync("KeplerCookies");
        HttpContext.Session.Clear();
        return RedirectToAction(nameof(Login));
    }
}
