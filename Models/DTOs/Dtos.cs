namespace TicketsKeplerTickets.Models.DTOs;

// ─── Auth ────────────────────────────────────────────────────────────────────

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

public class RefreshTokenDto
{
    public string RefreshToken { get; set; } = string.Empty;
}

public class RefreshResult
{
    public string AccessToken  { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
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

// ─── Profile ─────────────────────────────────────────────────────────────────

public class UserProfileDto
{
    public string FullName { get; set; } = string.Empty;
    public string Email    { get; set; } = string.Empty;
    public string? PhotoUrl { get; set; }
}

public class ChangePasswordDto
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword     { get; set; } = string.Empty;
}

// ─── Events ──────────────────────────────────────────────────────────────────
// Matches API: Movie=0, Concert=1, Theater=2, Sports=3, Other=4

public enum EventType { Movie = 0, Concert = 1, Theater = 2, Sports = 3, Other = 4 }

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

// ─── Showtimes ────────────────────────────────────────────────────────────────
// Matches API: Active=0, Cancelled=1, Completed=2, SoldOut=3

public enum ShowtimeStatus { Active = 0, Cancelled = 1, Completed = 2, SoldOut = 3 }

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
// Matches API exactly

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

// API returns: ReservedSeatIds (NOT SeatIds), ExpiresAt is nullable
public class ReservationResult
{
    public bool      Success         { get; set; }
    public string    Message         { get; set; } = string.Empty;
    public List<int> ReservedSeatIds { get; set; } = new();
    public DateTime? ExpiresAt       { get; set; }
}

// ─── Orders ──────────────────────────────────────────────────────────────────
// Matches API: Pending=0, Paid=1, Cancelled=2 (no Refunded)

public enum OrderStatus { Pending = 0, Paid = 1, Cancelled = 2 }

public class CreateOrderRequest
{
    public int       ShowtimeId { get; set; }
    public List<int> SeatIds    { get; set; } = new();
}

public class PayOrderRequest
{
    public int    OrderId       { get; set; }
    public string PaymentMethod { get; set; } = "CreditCard";
}

// Matches API OrderItemDto exactly
public class OrderItemDto
{
    public int      Id            { get; set; }
    public int      SeatId        { get; set; }  // used to release seat on pending cancel
    public string   SeatLabel     { get; set; } = string.Empty;
    public string   EventName     { get; set; } = string.Empty;
    public DateTime ShowtimeStart { get; set; }
    public decimal  PricePaid     { get; set; }
    public string?  QRCode        { get; set; }
    public string?  QrImageUrl    { get; set; }
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

// Matches API OrderTicketDto exactly
public class OrderTicketDto
{
    public int       TicketId      { get; set; }
    public string    QRCode        { get; set; } = string.Empty;
    public string?   QrImageUrl    { get; set; }
    public string    SeatLabel     { get; set; } = string.Empty;
    public string    EventName     { get; set; } = string.Empty;
    public DateTime  ShowtimeStart { get; set; }
    public bool      IsUsed        { get; set; }
    public DateTime? UsedAt        { get; set; }
}

// Matches API TicketSummaryDto (used inside PaymentResultDto)
public class TicketSummaryDto
{
    public int      TicketId      { get; set; }
    public string   QRCode        { get; set; } = string.Empty;
    public string?  QrImageUrl    { get; set; }
    public string   SeatLabel     { get; set; } = string.Empty;
    public string   EventName     { get; set; } = string.Empty;
    public DateTime ShowtimeStart { get; set; }
}

// Matches API PaymentResultDto exactly
public class PaymentResultDto
{
    public bool      Success       { get; set; }
    public string    TransactionId { get; set; } = string.Empty;
    public decimal   AmountPaid    { get; set; }
    public DateTime  PaidAt        { get; set; }
    public List<TicketSummaryDto> Tickets { get; set; } = new();
}

// Refund (API has /refund endpoint, not /cancel)
public class RefundRequestDto
{
    public string Reason { get; set; } = string.Empty;
}

public class RefundResultDto
{
    public int      RefundRequestId { get; set; }
    public int      OrderId         { get; set; }
    public int      Status          { get; set; }
    public string   Reason          { get; set; } = string.Empty;
    public DateTime RequestedAt     { get; set; }
}

// ─── Shared ──────────────────────────────────────────────────────────────────

public class ApiResponse<T>
{
    public bool         Success { get; set; }
    public string       Message { get; set; } = string.Empty;
    public T?           Data    { get; set; }
    public List<string> Errors  { get; set; } = new();
}

public class PagedResult<T>
{
    public List<T> Items      { get; set; } = new();
    public int     TotalCount { get; set; }
    public int     Page       { get; set; }
    public int     PageSize   { get; set; }
    public int     TotalPages { get; set; }
}