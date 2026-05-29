using System.Security.Claims;
using TicketsKeplerTickets.Models.DTOs;
using TicketsKeplerTickets.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TicketsKeplerTickets.Controllers;

[Authorize]
public class OrdersController : Controller
{
    private readonly IApiService _api;
    public OrdersController(IApiService api) => _api = api;

    private string Token => User.FindFirstValue("AccessToken") ?? string.Empty;

    // ─── Reserve seats (AJAX) ────────────────────────────────────────────────
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Reserve([FromBody] ReserveSeatsRequest req)
    {
        var result = await _api.ReserveSeatsAsync(req, Token);
        if (result == null || !result.Success)
            return BadRequest(new { message = result?.Message ?? "No se pudo reservar." });
        return Ok(result.Data);
    }

    // ─── Create order (AJAX) ─────────────────────────────────────────────────
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest req)
    {
        var result = await _api.CreateOrderAsync(req, Token);
        if (result == null || !result.Success)
            return BadRequest(new { message = result?.Message ?? "No se pudo crear la orden." });
        return Ok(result.Data);
    }

    // ─── Pay order (AJAX) ────────────────────────────────────────────────────
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Pay([FromBody] PayOrderRequest req)
    {
        var result = await _api.PayOrderAsync(req, Token);
        if (result == null || !result.Success)
            return BadRequest(new { message = result?.Message ?? "Error en el pago." });
        return Ok(result.Data);
    }

    // ─── Release seats (AJAX) ────────────────────────────────────────────────
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Release([FromBody] List<int> seatIds)
    {
        await _api.ReleaseSeatsAsync(seatIds, Token);
        return Ok();
    }

    // ─── My orders ───────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> MyOrders()
    {
        var orders = await _api.GetMyOrdersAsync(Token);
        return View(orders?.Items ?? new());
    }

    // ─── Order detail + tickets ──────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Detail(int id)
    {
        var order = await _api.GetOrderByIdAsync(id, Token);
        if (order == null) return NotFound();

        var tickets = await _api.GetOrderTicketsAsync(id, Token);
        ViewBag.Tickets = tickets ?? new List<OrderTicketDto>();

        return View(order);
    }
}
