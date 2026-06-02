using System.Security.Claims;
using TicketsKeplerTickets.Models.DTOs;
using TicketsKeplerTickets.Models.ViewModels;
using TicketsKeplerTickets.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TicketsKeplerTickets.Controllers;

[Authorize]
public class ProfileController : Controller
{
    private readonly IApiService _api;
    public ProfileController(IApiService api) => _api = api;

    private string Token => User.FindFirstValue("AccessToken") ?? string.Empty;

    // ─── Settings page ───────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Settings()
    {
        var profile = await _api.GetProfileAsync(Token);
        ViewBag.Profile = profile;
        ViewBag.ChangePasswordModel = new ChangePasswordViewModel();
        return View();
    }

    // ─── Change password (AJAX) ──────────────────────────────────────────────
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
    {
        var result = await _api.ChangePasswordAsync(dto, Token);
        if (result == null || !result.Success)
            return BadRequest(new { message = result?.Message ?? "Error al cambiar contraseña." });
        return Ok(new { message = result.Message });
    }

    // ─── Get photo URL (for nav avatar, called on page load) ─────────────────
    [HttpGet]
    public async Task<IActionResult> PhotoUrl()
    {
        var profile = await _api.GetProfileAsync(Token);
        return Ok(new { photoUrl = profile?.PhotoUrl ?? "" });
    }

    // ─── Upload photo (AJAX) ─────────────────────────────────────────────────
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> UploadPhoto(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "No se recibió ningún archivo." });

        if (file.Length > 5 * 1024 * 1024)
            return BadRequest(new { message = "La imagen no puede superar 5 MB." });

        var allowed = new[] { "image/jpeg", "image/png", "image/webp", "image/gif" };
        if (!allowed.Contains(file.ContentType.ToLower()))
            return BadRequest(new { message = "Solo se permiten imágenes JPG, PNG, WEBP o GIF." });

        using var stream = file.OpenReadStream();
        var result = await _api.UploadPhotoAsync(stream, file.FileName, file.ContentType, Token);
        if (result == null || !result.Success)
            return BadRequest(new { message = result?.Message ?? "Error al subir la foto." });

        // result.Message contains the photoUrl returned by the API
        // Return it so the JS can update the <img> src immediately
        return Ok(new { photoUrl = result.Message });
    }
}
