using TicketsKeplerTickets.Models.ViewModels;
using TicketsKeplerTickets.Services;
using Microsoft.AspNetCore.Mvc;

namespace TicketsKeplerTickets.Controllers;

public class EventsController : Controller
{
    private readonly IApiService _api;
    public EventsController(IApiService api) => _api = api;

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

    public async Task<IActionResult> SelectSeats(int showtimeId)
    {
        var showtime = await _api.GetShowtimeByIdAsync(showtimeId);
        if (showtime == null) return NotFound();

        var ev = await _api.GetEventByIdAsync(showtime.EventId);
        if (ev == null) return NotFound();

        var seats = await _api.GetShowtimeSeatsAsync(showtimeId);

        return View(new SeatSelectionViewModel
        {
            Showtime = showtime,
            Event    = ev,
            Seats    = seats ?? new()
        });
    }
}
