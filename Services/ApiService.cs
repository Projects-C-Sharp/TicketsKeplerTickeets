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

    // Profile
    Task<UserProfileDto?> GetProfileAsync(string token);
    Task<ApiResponse<object>?> ChangePasswordAsync(ChangePasswordDto dto, string token);
    Task<ApiResponse<object>?> UploadPhotoAsync(Stream fileStream, string fileName, string contentType, string token);

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
    // API uses /refund (not /cancel) for paid orders; pending orders have no cancel endpoint
    Task<ApiResponse<RefundResultDto>?> RequestRefundAsync(int orderId, string reason, string token);
    // Best-effort cancel for pending orders (API may not support it; non-fatal)
    Task<bool> CancelOrderAsync(int orderId, string token);
    Task<RefreshResult?> RefreshTokenAsync(string refreshToken);
}

public class ApiService : IApiService
{
    private readonly HttpClient _http;
    // CamelCase for serializing requests TO the API (e.g. "seatIds" not "SeatIds")
    // CaseInsensitive for deserializing responses FROM the API
    private readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
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

    private async Task<T?> PostAsync<T>(string url, object? body, string? token = null)
    {
        if (token != null) SetAuth(token);
        var content = body != null ? Json(body) : null;
        var resp = await _http.PostAsync(url, content);
        var json = await resp.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(json)) return default;
        try { return JsonSerializer.Deserialize<T>(json, _json); } catch { return default; }
    }

    // ─── Auth ─────────────────────────────────────────────────────────────────

    public async Task<ApiResponse<LoginResult>?> LoginAsync(LoginDto dto)
    {
        try
        {
            var resp = await _http.PostAsync("api/auth/login", Json(dto));
            var json = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                string message = "Credenciales incorrectas";
                try
                {
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("message", out var m))
                        message = m.GetString() ?? message;
                }
                catch { }
                return new ApiResponse<LoginResult> { Success = false, Message = message };
            }

            using var root = JsonDocument.Parse(json);
            var accessToken  = root.RootElement.GetProperty("accessToken").GetString()  ?? "";
            var refreshToken = root.RootElement.GetProperty("refreshToken").GetString() ?? "";

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

            // API can return:
            // 1. Plain string: "User already exists"
            // 2. IdentityError[]: [{ "code": "...", "description": "..." }]
            string message = "Error al registrar. Intenta de nuevo.";
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == JsonValueKind.String)
                {
                    var raw = doc.RootElement.GetString() ?? "";
                    message = raw == "User already exists"
                        ? "Ya existe una cuenta con ese correo electrónico."
                        : raw;
                }
                else if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    // IdentityError[] — collect all descriptions
                    var errors = new List<string>();
                    foreach (var el in doc.RootElement.EnumerateArray())
                    {
                        if (el.TryGetProperty("description", out var desc))
                        {
                            var d = desc.GetString() ?? "";
                            // Translate common Identity error codes to Spanish
                            errors.Add(TranslateIdentityError(d));
                        }
                    }
                    if (errors.Count > 0) message = string.Join(" ", errors);
                }
                else if (doc.RootElement.TryGetProperty("message", out var m))
                {
                    message = m.GetString() ?? message;
                }
            }
            catch { }

            return new ApiResponse<object> { Success = false, Message = message };
        }
        catch (Exception ex)
        {
            return new ApiResponse<object> { Success = false, Message = ex.Message };
        }
    }

    private static string TranslateIdentityError(string description) => description switch
    {
        var d when d.Contains("already taken")           => "Ese nombre de usuario ya está en uso.",
        var d when d.Contains("is already taken")        => "Ese correo ya está registrado.",
        var d when d.Contains("Passwords must have")     => "La contraseña debe tener al menos un carácter especial.",
        var d when d.Contains("least one digit")         => "La contraseña debe contener al menos un número.",
        var d when d.Contains("least one uppercase")     => "La contraseña debe tener al menos una mayúscula.",
        var d when d.Contains("least one lowercase")     => "La contraseña debe tener al menos una minúscula.",
        var d when d.Contains("least one non")           => "La contraseña debe tener al menos un carácter especial.",
        var d when d.Contains("too short")               => "La contraseña es demasiado corta (mínimo 8 caracteres).",
        var d when d.Contains("Invalid token")           => "El enlace es inválido o ha expirado.",
        _ => description
    };

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
            catch { }

            return new ApiResponse<object> { Success = false, Message = message };
        }
        catch { return new ApiResponse<object> { Success = false, Message = "Error de conexión" }; }
    }

    public async Task LogoutAsync(string token)
    {
        SetAuth(token);
        await _http.PostAsync("api/auth/logout", null);
    }

    // ─── Profile ──────────────────────────────────────────────────────────────

    public async Task<UserProfileDto?> GetProfileAsync(string token)
    {
        try
        {
            SetAuth(token);
            var resp = await _http.GetAsync("api/auth/me");
            if (!resp.IsSuccessStatusCode) return null;
            var json = await resp.Content.ReadAsStringAsync();

            // Try wrapped response: { "success": true, "data": { "fullName": ..., "photoUrl": ... } }
            try
            {
                var wrapped = JsonSerializer.Deserialize<ApiResponse<UserProfileDto>>(json, _json);
                if (wrapped?.Data != null) return wrapped.Data;
            }
            catch { }

            // Try direct/flat response: { "fullName": ..., "email": ..., "photoUrl": ... }
            try
            {
                var direct = JsonSerializer.Deserialize<UserProfileDto>(json, _json);
                if (direct != null) return direct;
            }
            catch { }

            return null;
        }
        catch { return null; }
    }

    public async Task<ApiResponse<object>?> ChangePasswordAsync(ChangePasswordDto dto, string token)
    {
        try
        {
            SetAuth(token);
            var resp = await _http.PutAsync("api/auth/change-password", Json(dto));
            var json = await resp.Content.ReadAsStringAsync();
            if (resp.IsSuccessStatusCode)
                return new ApiResponse<object> { Success = true, Message = "Contraseña actualizada" };
            string message = "Error al cambiar contraseña";
            try { using var doc = JsonDocument.Parse(json); if (doc.RootElement.TryGetProperty("message", out var m)) message = m.GetString() ?? message; } catch { }
            return new ApiResponse<object> { Success = false, Message = message };
        }
        catch (Exception ex) { return new ApiResponse<object> { Success = false, Message = ex.Message }; }
    }

    public async Task<ApiResponse<object>?> UploadPhotoAsync(Stream fileStream, string fileName, string contentType, string token)
    {
        try
        {
            // Use HttpRequestMessage so the auth token is scoped to THIS request only
            // (avoids race conditions on shared DefaultRequestHeaders)
            using var multipart = new MultipartFormDataContent();
            var streamContent = new StreamContent(fileStream);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            multipart.Add(streamContent, "file", fileName);

            using var request = new HttpRequestMessage(HttpMethod.Post, "api/auth/upload-photo");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = multipart;

            var resp = await _http.SendAsync(request);
            var json = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                string errMsg = "Error al subir foto";
                try
                {
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.ValueKind == JsonValueKind.String)
                        errMsg = doc.RootElement.GetString() ?? errMsg;
                    else if (doc.RootElement.TryGetProperty("message", out var m))
                        errMsg = m.GetString() ?? errMsg;
                }
                catch { }
                return new ApiResponse<object> { Success = false, Message = errMsg };
            }

            // API returns: { "photoUrl": "http://..." }
            string photoUrl = "";
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("photoUrl", out var pu))
                    photoUrl = pu.GetString() ?? "";
            }
            catch { }
            return new ApiResponse<object> { Success = true, Message = photoUrl };
        }
        catch (Exception ex) { return new ApiResponse<object> { Success = false, Message = ex.Message }; }
    }

    // ─── Events ───────────────────────────────────────────────────────────────

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

    // ─── Showtimes ────────────────────────────────────────────────────────────

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

    // ─── Seats ────────────────────────────────────────────────────────────────

    public async Task<ApiResponse<ReservationResult>?> ReserveSeatsAsync(ReserveSeatsRequest req, string token) =>
        await PostAsync<ApiResponse<ReservationResult>>("api/seats/reserve", req, token);

    public async Task ReleaseSeatsAsync(List<int> seatIds, string token)
    {
        SetAuth(token);
        await _http.PostAsync("api/seats/release", Json(seatIds));
    }

    // ─── Orders ───────────────────────────────────────────────────────────────

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

    // ── Token Refresh ────────────────────────────────────────────────────────
    // POST api/auth/refresh  { refreshToken: "..." }
    // Returns: { accessToken, refreshToken }
    public async Task<RefreshResult?> RefreshTokenAsync(string refreshToken)
    {
        try
        {
            // Do NOT set auth header — this endpoint is [AllowAnonymous]
            var resp = await _http.PostAsync("api/auth/refresh", Json(new RefreshTokenDto { RefreshToken = refreshToken }));
            var json = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode) return null;
            using var doc = JsonDocument.Parse(json);
            var at = doc.RootElement.GetProperty("accessToken").GetString()  ?? "";
            var rt = doc.RootElement.GetProperty("refreshToken").GetString() ?? "";
            return new RefreshResult { AccessToken = at, RefreshToken = rt };
        }
        catch { return null; }
    }

    // API endpoint: POST api/orders/{id}/refund  (not /cancel)
    public async Task<ApiResponse<RefundResultDto>?> RequestRefundAsync(int orderId, string reason, string token)
    {
        try
        {
            SetAuth(token);
            var resp = await _http.PostAsync($"api/orders/{orderId}/refund", Json(new RefundRequestDto { Reason = reason }));
            var json = await resp.Content.ReadAsStringAsync();
            try { return JsonSerializer.Deserialize<ApiResponse<RefundResultDto>>(json, _json); } catch { return default; }
        }
        catch (Exception ex) { return new ApiResponse<RefundResultDto> { Success = false, Message = ex.Message }; }
    }

    // Best-effort: attempt to cancel the order via API.
    // Returns true if the API accepted it; false otherwise (non-fatal — seats are released separately).
    public async Task<bool> CancelOrderAsync(int orderId, string token)
    {
        try
        {
            SetAuth(token);
            // Try common cancel endpoints — the API may expose one of these
            var resp = await _http.PostAsync($"api/orders/{orderId}/cancel", null);
            if (resp.IsSuccessStatusCode) return true;
            // Some APIs use DELETE for cancel
            var del = await _http.DeleteAsync($"api/orders/{orderId}");
            return del.IsSuccessStatusCode;
        }
        catch { return false; }
    }
}