namespace TicketsKeplerTickets.Models.DTOs;

// ─── Auth ───────────────────────────────────────────────────────────────────

public class RegisterDto
{
    public string FullName { get; set; } = string.Empty;
    public string Email    { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginDto
{
    public string Email    { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class ForgotPasswordRequest
{
    public string Email { get; set; } = string.Empty;
}

public class ResetPasswordRequest
{
    public string Email       { get; set; } = string.Empty;
    public string Token       { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

public class LoginResult
{
    public string AccessToken  { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string Email        { get; set; } = string.Empty;
    public string FullName     { get; set; } = string.Empty;
    public List<string> Roles  { get; set; } = new();
}

// ─── Events ─────────────────────────────────────────────────────────────────

public enum EventType { Concert = 0, Theater = 1, Sports = 2, Conference = 3, Other = 4 }

public class EventDto
{
    public int       Id              { get; set; }
    public string    Name            { get; set; } = string.Empty;
    public string    Description     { get; set; } = string.Empty;
    public string?   PosterUrl       { get; set; }
    public string    VenueName       { get; set; } = string.Empty;
    public string    VenueCity       { get; set; } = string.Empty;
    public EventType Type            { get; set; }
    public int       DurationMinutes { get; set; }
    public bool      IsActive        { get; set; }
    public DateTime  CreatedAt       { get; set; }
}

// ─── Showtimes ───────────────────────────────────────────────────────────────

public enum ShowtimeStatus { Scheduled = 0, Active = 1, Completed = 2, Cancelled = 3 }

public class ShowtimeDto
{
    public int            Id             { get; set; }
    public int            EventId        { get; set; }
    public string         EventName      { get; set; } = string.Empty;
    public DateTime       StartTime      { get; set; }
    public DateTime       EndTime        { get; set; }
    public decimal        BasePrice      { get; set; }
    public ShowtimeStatus Status         { get; set; }
    public int            AvailableSeats { get; set; }
    public int            TotalSeats     { get; set; }
}

// ─── Seats ───────────────────────────────────────────────────────────────────

public enum SeatStatus { Available = 0, Reserved = 1, Sold = 2 }
public enum SeatType   { Standard = 0, Premium = 1, VIP = 2 }

public class SeatDto
{
    public int        Id            { get; set; }
    public string     Row           { get; set; } = string.Empty;
    public int        Number        { get; set; }
    public string     Label         { get; set; } = string.Empty;
    public SeatType   Type          { get; set; }
    public SeatStatus Status        { get; set; }
    public DateTime?  ReservedUntil { get; set; }
}

public class ReserveSeatsRequest
{
    public int       ShowtimeId { get; set; }
    public List<int> SeatIds    { get; set; } = new();
}

public class ReservationResult
{
    public bool     Success   { get; set; }
    public string   Message   { get; set; } = string.Empty;
    public List<int> SeatIds  { get; set; } = new();
    public DateTime ExpiresAt { get; set; }
}

// ─── Orders ──────────────────────────────────────────────────────────────────

public enum OrderStatus { Pending = 0, Paid = 1, Cancelled = 2, Refunded = 3 }
public enum PaymentStatus { Pending = 0, Approved = 1, Rejected = 2 }

public class CreateOrderRequest
{
    public List<int> SeatIds { get; set; } = new();
}

public class PayOrderRequest
{
    public int    OrderId       { get; set; }
    public string PaymentMethod { get; set; } = "CreditCard";
}

public class OrderItemDto
{
    public int     SeatId    { get; set; }
    public string  SeatLabel { get; set; } = string.Empty;
    public decimal Price     { get; set; }
}

public class OrderDto
{
    public int              Id        { get; set; }
    public string           UserEmail { get; set; } = string.Empty;
    public decimal          Total     { get; set; }
    public OrderStatus      Status    { get; set; }
    public DateTime         CreatedAt { get; set; }
    public List<OrderItemDto> Items   { get; set; } = new();
}

public class OrderTicketDto
{
    public int    Id        { get; set; }
    public string SeatLabel { get; set; } = string.Empty;
    public string QrCodeUrl { get; set; } = string.Empty;
    public string EventName { get; set; } = string.Empty;
    public DateTime ShowtimeStart { get; set; }
}

public class PaymentResultDto
{
    public int    OrderId       { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public decimal Amount       { get; set; }
    public List<OrderTicketDto> Tickets { get; set; } = new();
}

// ─── Shared ──────────────────────────────────────────────────────────────────

public class ApiResponse<T>
{
    public bool   Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public T?     Data    { get; set; }
}

public class PagedResult<T>
{
    public List<T> Items      { get; set; } = new();
    public int     TotalCount { get; set; }
    public int     Page       { get; set; }
    public int     PageSize   { get; set; }
    public int     TotalPages { get; set; }
}
