using TicketsKeplerTickets.Services;
using Microsoft.AspNetCore.Mvc;

namespace TicketsKeplerTickets.Controllers;

public class HomeController : Controller
{
    private readonly IApiService _api;
    public HomeController(IApiService api) => _api = api;

    public async Task<IActionResult> Index()
    {
        var events = await _api.GetEventsAsync(1, 6);
        return View(events?.Items ?? new());
    }
}
