using System.ComponentModel.DataAnnotations;

namespace TicketsKeplerTickets.Models.ViewModels;

// ─── Auth ────────────────────────────────────────────────────────────────────

public class LoginViewModel
{
    [Required(ErrorMessage = "El correo es requerido")]
    [EmailAddress(ErrorMessage = "Correo no válido")]
    [Display(Name = "Correo electrónico")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contraseña es requerida")]
    [DataType(DataType.Password)]
    [Display(Name = "Contraseña")]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}

public class RegisterViewModel
{
    [Required(ErrorMessage = "El nombre completo es requerido")]
    [Display(Name = "Nombre completo")]
    [StringLength(100, MinimumLength = 3)]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "El correo es requerido")]
    [EmailAddress(ErrorMessage = "Correo no válido")]
    [Display(Name = "Correo electrónico")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contraseña es requerida")]
    [DataType(DataType.Password)]
    [MinLength(6, ErrorMessage = "Mínimo 6 caracteres")]
    [Display(Name = "Contraseña")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirma tu contraseña")]
    [DataType(DataType.Password)]
    [Compare("Password", ErrorMessage = "Las contraseñas no coinciden")]
    [Display(Name = "Confirmar contraseña")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class ForgotPasswordViewModel
{
    [Required(ErrorMessage = "El correo es requerido")]
    [EmailAddress(ErrorMessage = "Correo no válido")]
    [Display(Name = "Correo electrónico")]
    public string Email { get; set; } = string.Empty;
}

public class ResetPasswordViewModel
{
    [Required]
    public string Email { get; set; } = string.Empty;
    [Required]
    public string Token { get; set; } = string.Empty;
    [Required(ErrorMessage = "La nueva contraseña es requerida")]
    [DataType(DataType.Password)]
    [MinLength(6, ErrorMessage = "Mínimo 6 caracteres")]
    [Display(Name = "Nueva contraseña")]
    public string NewPassword { get; set; } = string.Empty;
    [Required(ErrorMessage = "Confirma tu contraseña")]
    [DataType(DataType.Password)]
    [Compare("NewPassword", ErrorMessage = "Las contraseñas no coinciden")]
    [Display(Name = "Confirmar contraseña")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

// ─── Events / Showtimes ──────────────────────────────────────────────────────

public class EventDetailViewModel
{
    public TicketsKeplerTickets.Models.DTOs.EventDto Event { get; set; } = new();
    public List<TicketsKeplerTickets.Models.DTOs.ShowtimeDto> Showtimes { get; set; } = new();
}

// ─── Seat Selection ──────────────────────────────────────────────────────────

public class SeatSelectionViewModel
{
    public TicketsKeplerTickets.Models.DTOs.ShowtimeDto Showtime { get; set; } = new();
    public TicketsKeplerTickets.Models.DTOs.EventDto Event       { get; set; } = new();
    public List<TicketsKeplerTickets.Models.DTOs.SeatDto> Seats  { get; set; } = new();
    // Non-null when the authenticated user has a Pending order for this showtime
    public TicketsKeplerTickets.Models.DTOs.OrderDto? PendingOrder { get; set; }
}

// ─── Profile / Settings ─────────────────────────────────────────────────────

public class ChangePasswordViewModel
{
    [Required(ErrorMessage = "La contraseña actual es requerida")]
    [DataType(DataType.Password)]
    [Display(Name = "Contraseña actual")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "La nueva contraseña es requerida")]
    [DataType(DataType.Password)]
    [MinLength(6, ErrorMessage = "Mínimo 6 caracteres")]
    [Display(Name = "Nueva contraseña")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirma tu nueva contraseña")]
    [DataType(DataType.Password)]
    [Compare("NewPassword", ErrorMessage = "Las contraseñas no coinciden")]
    [Display(Name = "Confirmar contraseña")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
