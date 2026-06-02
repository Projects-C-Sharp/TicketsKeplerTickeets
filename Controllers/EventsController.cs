using System.Security.Claims;
using TicketsKeplerTickets.Models.DTOs;
using TicketsKeplerTickets.Models.ViewModels;
using TicketsKeplerTickets.Services;
using Microsoft.AspNetCore.Mvc;

namespace TicketsKeplerTickets.Controllers;

public class EventsController : Controller
{
    private readonly IApiService _api;
    public EventsController(IApiService api) => _api = api;

    private string Token => User.FindFirstValue("AccessToken") ?? string.Empty;

    public async Task<IActionResult> Index(int page = 1)
    {
        var events = await _api.GetEventsAsync(page, 9);
        return View(events);
    }

    public async Task<IActionResult> Detail(int id)
    {
        var ev = await _api.GetEventByIdAsync(id);
        if (ev == null) return NotFound();

        var showtimes = await _api.GetShowtimesAsync(id);

        return View(new EventDetailViewModel
        {
            Event     = ev,
            Showtimes = showtimes?.Items ?? new()
        });
    }

    // ── Real-time seat polling endpoint ───────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> SeatsJson(int showtimeId)
    {
        var seats = await _api.GetShowtimeSeatsAsync(showtimeId);
        if (seats == null) return NotFound();
        var payload = seats.Select(s => new { id = s.Id, status = (int)s.Status });
        return Json(payload);
    }

    public async Task<IActionResult> SelectSeats(int showtimeId)
    {
        var showtime = await _api.GetShowtimeByIdAsync(showtimeId);
        if (showtime == null) return NotFound();

        // Block past showtimes — users cannot buy tickets for events that already happened
        if (showtime.StartTime <= DateTime.UtcNow)
        {
            TempData["ErrorMessage"] = "Esta función ya ocurrió y no está disponible para compra.";
            return RedirectToAction("Detail", new { id = showtime.EventId });
        }

        var ev = await _api.GetEventByIdAsync(showtime.EventId);
        if (ev == null) return NotFound();

        var seats = await _api.GetShowtimeSeatsAsync(showtimeId);

        // If the user is logged in, check whether they have a Pending order for this showtime.
        // We also verify that at least one of its seats is still Reserved in the current seat map;
        // this prevents a cancelled-but-not-marked order (seats already released) from locking
        // the user out of the seat selection screen after they cancel a reservation.
        OrderDto? pendingOrder = null;
        if (User.Identity?.IsAuthenticated == true && !string.IsNullOrEmpty(Token))
        {
            try
            {
                var myOrders = await _api.GetMyOrdersAsync(Token);
                var candidate = myOrders?.Items.FirstOrDefault(o =>
                    o.Status == OrderStatus.Pending &&
                    o.Items.Any(i => i.ShowtimeStart == showtime.StartTime));

                if (candidate != null)
                {
                    // Only treat the order as pending if its seats are still Reserved in the
                    // live seat map. If all seats were released (e.g. after CancelPending),
                    // the order is effectively dead even if the API still shows it as Pending.
                    var pendingSeatIds = candidate.Items
                        .Where(i => i.SeatId > 0)
                        .Select(i => i.SeatId)
                        .ToHashSet();

                    bool seatsStillReserved = seats != null &&
                        seats.Any(s => pendingSeatIds.Contains(s.Id) && s.Status == SeatStatus.Reserved);

                    if (seatsStillReserved)
                        pendingOrder = candidate;
                }
            }
            catch { /* non-critical — proceed without pending order */ }
        }

        return View(new SeatSelectionViewModel
        {
            Showtime     = showtime,
            Event        = ev,
            Seats        = seats ?? new(),
            PendingOrder = pendingOrder
        });
    }
}