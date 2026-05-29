# Kepler Tickets — kepler.andrescortes.dev

Aplicación web ASP.NET Core 8 MVC para la venta de tickets al público (rol **Customer**).  
Consume la API central en `https://api.kepler.andrescortes.dev`.

---

## Stack

| Capa | Tecnología |
|---|---|
| Backend | ASP.NET Core 8 MVC, C# |
| Vistas | Razor + ViewComponents |
| Autenticación | Cookie Auth + JWT (almacenado en sesión) |
| API Client | HttpClient tipado (`ApiService`) |
| Estilos | CSS custom (sin Bootstrap) |
| Fonts | Syne + DM Sans (Google Fonts) |

---

## Estructura del proyecto

```
KeplerTickets/
├── Controllers/
│   ├── AuthController.cs       # Login, Register, ForgotPassword, ResetPassword, Logout
│   ├── EventsController.cs     # Index, Detail, SelectSeats
│   ├── HomeController.cs       # Landing page
│   └── OrdersController.cs     # Reserve, Create, Pay, Release, MyOrders, Detail
├── Models/
│   ├── DTOs/Dtos.cs            # Todos los DTOs que mapean la API
│   └── ViewModels/ViewModels.cs # ViewModels con DataAnnotations
├── Services/
│   └── ApiService.cs           # Cliente HTTP tipado para la API central
├── Views/
│   ├── Auth/                   # Login, Register, ForgotPassword, ResetPassword
│   ├── Events/                 # Index, Detail, SelectSeats (mapa de asientos)
│   ├── Home/Index.cshtml       # Landing con eventos destacados
│   ├── Orders/                 # MyOrders, Detail (tickets QR)
│   └── Shared/                 # _Layout, _AuthLayout, _ValidationScriptsPartial
└── wwwroot/
    ├── css/site.css            # Estilos completos (dark theme, paleta azul/dorado)
    ├── js/site.js              # Nav toggle, toasts
    └── js/seats.js             # Lógica del mapa de asientos (reserva → orden → pago)
```

---

## Cómo correr localmente

### Requisitos
- .NET 8 SDK: https://dotnet.microsoft.com/download/dotnet/8.0
- La API `https://api.kepler.andrescortes.dev` debe estar disponible

### Pasos

```bash
# 1. Restaurar paquetes
dotnet restore

# 2. Correr en desarrollo
dotnet run --launch-profile https

# 3. Abrir en el navegador
# https://localhost:7200
```

---

## Flujo de usuario

```
Landing (/) → Eventos (/Events) → Detalle del evento → Seleccionar función
    → Mapa de asientos (SelectSeats) → Reservar (Redis 5 min) → Crear orden
    → Elegir método de pago → Pagar → Modal de éxito → Mis Tickets (/Orders/MyOrders)
```

### Autenticación
- Solo usuarios con rol **Customer** pueden iniciar sesión
- Registro abierto en `/Auth/Register`
- Recuperación de contraseña por correo en `/Auth/ForgotPassword`

---

## Paleta de colores

| Token | Color | Uso |
|---|---|---|
| `--c-bg` | `#0a0d14` | Fondo principal |
| `--c-accent` | `#5b8df6` | Azul primario, botones, links |
| `--c-accent2` | `#7c5cbf` | Asientos Premium |
| `--c-gold` | `#e8b84b` | Precios, asientos VIP, etapas |
| `--c-green` | `#34d399` | Éxito, asientos seleccionados |
| `--c-red` | `#f87171` | Errores, asientos agotados |

---

## Despliegue en producción

```bash
dotnet publish -c Release -o ./publish

# Variables de entorno recomendadas:
# ASPNETCORE_ENVIRONMENT=Production
# ApiSettings__BaseUrl=https://api.kepler.andrescortes.dev
```

Para Nginx/proxy reverso, asegúrate de configurar `ForwardedHeaders`.
