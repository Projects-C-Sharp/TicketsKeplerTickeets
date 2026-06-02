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
        // Return data that matches what seats.js expects
        return Ok(new {
            success      = result.Data!.Success,
            reservedSeatIds = result.Data.ReservedSeatIds,
            expiresAt    = result.Data.ExpiresAt
        });
    }

    // ─── Create order (AJAX) ─────────────────────────────────────────────────
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest req)
    {
        var result = await _api.CreateOrderAsync(req, Token);
        if (result == null || !result.Success)
            return BadRequest(new { message = result?.Message ?? "No se pudo crear la orden." });
        // Explicit lowercase so JS can read orderId regardless of global serialization
        return Ok(new { id = result.Data!.Id, total = result.Data.Total, status = result.Data.Status });
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

    // ─── Cancel reservation only (AJAX) ──────────────────────────────────────
    // Releases the reserved seats without touching the order record.
    // Used in the two-step cancel flow: CancelReservation → CancelOrder.
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> CancelReservation([FromBody] CancelPendingRequest req)
    {
        var order = await _api.GetOrderByIdAsync(req.OrderId, Token);
        if (order == null)
            return NotFound(new { message = "Orden no encontrada." });

        var seatIds = order.Items
            .Where(i => i.SeatId > 0)
            .Select(i => i.SeatId)
            .ToList();

        if (seatIds.Count > 0)
            await _api.ReleaseSeatsAsync(seatIds, Token);

        return Ok(new { message = "Asientos liberados correctamente." });
    }

    // ─── Cancel order (AJAX) ─────────────────────────────────────────────────
    // Attempts to cancel the order via the API. If the API has no cancel
    // endpoint for Pending orders, we still return success so the UI reloads
    // (seats were already released by CancelReservation above).
    // EventsController.SelectSeats verifies seats are still Reserved before
    // treating an order as pending, so released seats make the order effectively dead.
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> CancelOrder([FromBody] CancelPendingRequest req)
    {
        var order = await _api.GetOrderByIdAsync(req.OrderId, Token);
        if (order == null)
            return NotFound(new { message = "Orden no encontrada." });

        if (order.Status != OrderStatus.Pending)
            return BadRequest(new { message = "Solo se pueden cancelar órdenes pendientes." });

        // Attempt API-level cancel (may not exist; failure is non-fatal since seats are already released)
        var result = await _api.CancelOrderAsync(req.OrderId, Token);
        // Always return Ok — the order is effectively dead once seats are released
        return Ok(new { message = "Orden cancelada." });
    }

    // ─── Legacy CancelPending (kept for backwards compat) ───────────────────
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> CancelPending([FromBody] CancelPendingRequest req)
    {
        var order = await _api.GetOrderByIdAsync(req.OrderId, Token);
        if (order == null)
            return NotFound(new { message = "Orden no encontrada." });

        var seatIds = order.Items
            .Where(i => i.SeatId > 0)
            .Select(i => i.SeatId)
            .ToList();

        if (seatIds.Count > 0)
            await _api.ReleaseSeatsAsync(seatIds, Token);

        return Ok(new { message = "Orden cancelada. Los asientos han sido liberados." });
    }

    // ─── Request refund (AJAX) ───────────────────────────────────────────────
    // Note: API only supports refund (for Paid orders), not cancel for Pending
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Refund([FromBody] RefundRequest req)
    {
        var result = await _api.RequestRefundAsync(req.OrderId, req.Reason, Token);
        if (result == null || !result.Success)
            return BadRequest(new { message = result?.Message ?? "No se pudo solicitar el reembolso." });
        return Ok(new { message = "Solicitud de reembolso enviada. Pendiente de revisión." });
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

// Helper DTO for refund request from JS
public class RefundRequest
{
    public int    OrderId { get; set; }
    public string Reason  { get; set; } = "Solicitud del cliente";
}

// Helper DTO for cancel pending request from JS
public class CancelPendingRequest
{
    public int OrderId { get; set; }
}