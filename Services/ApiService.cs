using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using TicketsKeplerTickets.Models.DTOs;

namespace TicketsKeplerTickets.Services;

public interface IApiService
{
    // Auth
    Task<ApiResponse<LoginResult>?> LoginAsync(LoginDto dto);
    Task<ApiResponse<object>?> RegisterCustomerAsync(RegisterDto dto);
    Task<ApiResponse<object>?> ForgotPasswordAsync(ForgotPasswordRequest req);
    Task<ApiResponse<object>?> ResetPasswordAsync(ResetPasswordRequest req);
    Task LogoutAsync(string token);

    // Events
    Task<PagedResult<EventDto>?> GetEventsAsync(int page = 1, int pageSize = 12);
    Task<EventDto?> GetEventByIdAsync(int id);

    // Showtimes
    Task<PagedResult<ShowtimeDto>?> GetShowtimesAsync(int? eventId = null);
    Task<ShowtimeDto?> GetShowtimeByIdAsync(int id);
    Task<List<SeatDto>?> GetShowtimeSeatsAsync(int showtimeId);

    // Seats
    Task<ApiResponse<ReservationResult>?> ReserveSeatsAsync(ReserveSeatsRequest req, string token);
    Task ReleaseSeatsAsync(List<int> seatIds, string token);

    // Orders
    Task<ApiResponse<OrderDto>?> CreateOrderAsync(CreateOrderRequest req, string token);
    Task<ApiResponse<PaymentResultDto>?> PayOrderAsync(PayOrderRequest req, string token);
    Task<PagedResult<OrderDto>?> GetMyOrdersAsync(string token);
    Task<OrderDto?> GetOrderByIdAsync(int id, string token);
    Task<List<OrderTicketDto>?> GetOrderTicketsAsync(int orderId, string token);
}

public class ApiService : IApiService
{
    private readonly HttpClient _http;
    private readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
    };

    public ApiService(HttpClient http) => _http = http;

    private void SetAuth(string token) =>
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private StringContent Json<T>(T obj) =>
        new(JsonSerializer.Serialize(obj, _json), Encoding.UTF8, "application/json");

    private async Task<T?> GetAsync<T>(string url, string? token = null)
    {
        if (token != null) SetAuth(token);
        var resp = await _http.GetAsync(url);
        if (!resp.IsSuccessStatusCode) return default;
        var json = await resp.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(json, _json);
    }

    private async Task<T?> PostAsync<T>(string url, object body, string? token = null)
    {
        if (token != null) SetAuth(token);
        var resp = await _http.PostAsync(url, Json(body));
        var json = await resp.Content.ReadAsStringAsync();
        try { return JsonSerializer.Deserialize<T>(json, _json); } catch { return default; }
    }

    // ─── Auth ────────────────────────────────────────────────────────────────

    public async Task<ApiResponse<LoginResult>?> LoginAsync(LoginDto dto) =>
        await PostAsync<ApiResponse<LoginResult>>("api/auth/login", dto);

    public async Task<ApiResponse<object>?> RegisterCustomerAsync(RegisterDto dto) =>
        await PostAsync<ApiResponse<object>>("api/auth/register-customer", dto);

    public async Task<ApiResponse<object>?> ForgotPasswordAsync(ForgotPasswordRequest req) =>
        await PostAsync<ApiResponse<object>>("api/auth/forgot-password", req);

    public async Task<ApiResponse<object>?> ResetPasswordAsync(ResetPasswordRequest req) =>
        await PostAsync<ApiResponse<object>>("api/auth/reset-password", req);

    public async Task LogoutAsync(string token)
    {
        SetAuth(token);
        await _http.PostAsync("api/auth/logout", null);
    }

    // ─── Events ──────────────────────────────────────────────────────────────

    public async Task<PagedResult<EventDto>?> GetEventsAsync(int page = 1, int pageSize = 12)
    {
        var resp = await GetAsync<ApiResponse<PagedResult<EventDto>>>($"api/events?page={page}&pageSize={pageSize}&isActive=true");
        return resp?.Data;
    }

    public async Task<EventDto?> GetEventByIdAsync(int id)
    {
        var resp = await GetAsync<ApiResponse<EventDto>>($"api/events/{id}");
        return resp?.Data;
    }

    // ─── Showtimes ───────────────────────────────────────────────────────────

    public async Task<PagedResult<ShowtimeDto>?> GetShowtimesAsync(int? eventId = null)
    {
        var url = eventId.HasValue
            ? $"api/showtimes?eventId={eventId}&pageSize=50"
            : "api/showtimes?pageSize=50";
        var resp = await GetAsync<ApiResponse<PagedResult<ShowtimeDto>>>(url);
        return resp?.Data;
    }

    public async Task<ShowtimeDto?> GetShowtimeByIdAsync(int id)
    {
        var resp = await GetAsync<ApiResponse<ShowtimeDto>>($"api/showtimes/{id}");
        return resp?.Data;
    }

    public async Task<List<SeatDto>?> GetShowtimeSeatsAsync(int showtimeId)
    {
        var resp = await GetAsync<ApiResponse<List<SeatDto>>>($"api/showtimes/{showtimeId}/seats");
        return resp?.Data;
    }

    // ─── Seats ───────────────────────────────────────────────────────────────

    public async Task<ApiResponse<ReservationResult>?> ReserveSeatsAsync(ReserveSeatsRequest req, string token) =>
        await PostAsync<ApiResponse<ReservationResult>>("api/seats/reserve", req, token);

    public async Task ReleaseSeatsAsync(List<int> seatIds, string token)
    {
        SetAuth(token);
        await _http.PostAsync("api/seats/release", Json(seatIds));
    }

    // ─── Orders ──────────────────────────────────────────────────────────────

    public async Task<ApiResponse<OrderDto>?> CreateOrderAsync(CreateOrderRequest req, string token) =>
        await PostAsync<ApiResponse<OrderDto>>("api/orders", req, token);

    public async Task<ApiResponse<PaymentResultDto>?> PayOrderAsync(PayOrderRequest req, string token) =>
        await PostAsync<ApiResponse<PaymentResultDto>>("api/orders/pay", req, token);

    public async Task<PagedResult<OrderDto>?> GetMyOrdersAsync(string token)
    {
        var resp = await GetAsync<ApiResponse<PagedResult<OrderDto>>>("api/orders?pageSize=50", token);
        return resp?.Data;
    }

    public async Task<OrderDto?> GetOrderByIdAsync(int id, string token)
    {
        var resp = await GetAsync<ApiResponse<OrderDto>>($"api/orders/{id}", token);
        return resp?.Data;
    }

    public async Task<List<OrderTicketDto>?> GetOrderTicketsAsync(int orderId, string token)
    {
        var resp = await GetAsync<ApiResponse<List<OrderTicketDto>>>($"api/orders/{orderId}/tickets", token);
        return resp?.Data;
    }
}
