using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
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
    // NOTE: The API returns flat responses for auth endpoints (no ApiResponse<T> wrapper).
    // Login returns { accessToken, refreshToken }; we decode the JWT to get claims.

    public async Task<ApiResponse<LoginResult>?> LoginAsync(LoginDto dto)
    {
        try
        {
            var resp = await _http.PostAsync("api/auth/login", Json(dto));
            var json = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                // Try to read an error message from the response body
                string message = "Credenciales incorrectas";
                try
                {
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("message", out var m))
                        message = m.GetString() ?? message;
                }
                catch { /* ignore parse errors */ }

                return new ApiResponse<LoginResult> { Success = false, Message = message };
            }

            // Parse the flat { accessToken, refreshToken } response
            using var root = JsonDocument.Parse(json);
            var accessToken  = root.RootElement.GetProperty("accessToken").GetString()  ?? "";
            var refreshToken = root.RootElement.GetProperty("refreshToken").GetString() ?? "";

            // Decode the JWT to extract claims (email, name, roles)
            var handler = new JwtSecurityTokenHandler();
            var jwt     = handler.ReadJwtToken(accessToken);

            var email    = jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email    || c.Type == "email")?.Value    ?? "";
            var fullName = jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name     || c.Type == "unique_name")?.Value ?? "";
            var roles    = jwt.Claims.Where(c => c.Type == ClaimTypes.Role || c.Type == "role").Select(c => c.Value).ToList();

            return new ApiResponse<LoginResult>
            {
                Success = true,
                Message = "OK",
                Data = new LoginResult
                {
                    AccessToken  = accessToken,
                    RefreshToken = refreshToken,
                    Email        = email,
                    FullName     = fullName,
                    Roles        = roles
                }
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<LoginResult> { Success = false, Message = ex.Message };
        }
    }

    public async Task<ApiResponse<object>?> RegisterCustomerAsync(RegisterDto dto)
    {
        try
        {
            var resp = await _http.PostAsync("api/auth/register-customer", Json(dto));
            var json = await resp.Content.ReadAsStringAsync();

            if (resp.IsSuccessStatusCode)
                return new ApiResponse<object> { Success = true, Message = "Registro exitoso" };

            string message = "Error al registrar";
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == JsonValueKind.String)
                    message = doc.RootElement.GetString() ?? message;
                else if (doc.RootElement.TryGetProperty("message", out var m))
                    message = m.GetString() ?? message;
            }
            catch { /* ignore */ }

            return new ApiResponse<object> { Success = false, Message = message };
        }
        catch (Exception ex)
        {
            return new ApiResponse<object> { Success = false, Message = ex.Message };
        }
    }

    public async Task<ApiResponse<object>?> ForgotPasswordAsync(ForgotPasswordRequest req)
    {
        try
        {
            var resp = await _http.PostAsync("api/auth/forgot-password", Json(req));
            return new ApiResponse<object> { Success = resp.IsSuccessStatusCode, Message = "OK" };
        }
        catch { return new ApiResponse<object> { Success = false, Message = "Error de conexión" }; }
    }

    public async Task<ApiResponse<object>?> ResetPasswordAsync(ResetPasswordRequest req)
    {
        try
        {
            var resp = await _http.PostAsync("api/auth/reset-password", Json(req));
            var json = await resp.Content.ReadAsStringAsync();

            if (resp.IsSuccessStatusCode)
                return new ApiResponse<object> { Success = true, Message = "OK" };

            string message = "El enlace expiró o es inválido";
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("message", out var m))
                    message = m.GetString() ?? message;
            }
            catch { /* ignore */ }

            return new ApiResponse<object> { Success = false, Message = message };
        }
        catch { return new ApiResponse<object> { Success = false, Message = "Error de conexión" }; }
    }

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