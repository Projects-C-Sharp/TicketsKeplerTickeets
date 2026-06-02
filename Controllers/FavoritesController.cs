using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using TicketsKeplerTickets.Models.DTOs;
using TicketsKeplerTickets.Services;

namespace TicketsKeplerTickets.Controllers;

public class FavoritesController : Controller
{
    private readonly IApiService _api;
    private const string CookieKey = "tx_favorites";

    public FavoritesController(IApiService api) => _api = api;

    // ── Helpers ──────────────────────────────────────────────────────────────

    private List<int> GetFavIds()
    {
        var raw = Request.Cookies[CookieKey];
        if (string.IsNullOrEmpty(raw)) return new();
        try { return JsonSerializer.Deserialize<List<int>>(raw) ?? new(); }
        catch { return new(); }
    }

    private void SaveFavIds(List<int> ids)
    {
        var opts = new CookieOptions
        {
            Expires     = DateTimeOffset.UtcNow.AddYears(1),
            IsEssential = true,
            SameSite    = SameSiteMode.Lax,
            HttpOnly    = false   // JS needs to read it too
        };
        Response.Cookies.Append(CookieKey, JsonSerializer.Serialize(ids), opts);
    }

    // ── Toggle (AJAX POST) ────────────────────────────────────────────────────
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public IActionResult Toggle([FromBody] FavToggleRequest req)
    {
        var ids   = GetFavIds();
        bool isFav;
        if (ids.Contains(req.EventId))
        {
            ids.Remove(req.EventId);
            isFav = false;
        }
        else
        {
            ids.Add(req.EventId);
            isFav = true;
        }
        SaveFavIds(ids);
        return Ok(new { isFav, count = ids.Count });
    }

    // ── List IDs (AJAX GET) ───────────────────────────────────────────────────
    [HttpGet]
    public IActionResult Ids() => Ok(GetFavIds());

    // ── Full list for profile tab (AJAX GET) ──────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Events()
    {
        var ids = GetFavIds();
        if (!ids.Any()) return Ok(new List<object>());

        var tasks   = ids.Select(id => _api.GetEventByIdAsync(id));
        var results = await Task.WhenAll(tasks);
        var events  = results
            .Where(e => e != null)
            .Select(e => new {
                e!.Id,
                e.Name,
                e.PosterUrl,
                e.VenueCity,
                e.VenueName,
                TypeLabel = e.Type switch {
                    EventType.Concert => "Concierto",
                    EventType.Theater => "Teatro",
                    EventType.Sports  => "Deporte",
                    EventType.Movie   => "Película",
                    _                 => "Evento"
                },
                TypeEmoji = e.Type switch {
                    EventType.Concert => "🎵",
                    EventType.Theater => "🎭",
                    EventType.Sports  => "⚽",
                    EventType.Movie   => "🎬",
                    _                 => "✨"
                }
            })
            .ToList();

        return Ok(events);
    }
}

public class FavToggleRequest
{
    public int EventId { get; set; }
}
