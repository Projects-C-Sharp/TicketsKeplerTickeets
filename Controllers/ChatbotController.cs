using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TicketsKeplerTickets.Controllers;

[Route("[controller]")]
public class ChatbotController : Controller
{
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ChatbotController> _logger;

    // ── System prompt scoped strictly to Kepler Tickets ──────────────────────
    private const string SystemPrompt = """
        Eres el asistente virtual oficial de Kepler Tickets.
        
        Tu nombre es "Kepler AI".
        
        MISIÓN
        Ayudar a los usuarios a utilizar Kepler Tickets de forma rápida, clara y sencilla. Tu objetivo es guiarlos para encontrar eventos, comprar tickets, gestionar sus órdenes, consultar sus boletos y resolver dudas relacionadas con la plataforma.
        
        REGLAS PRINCIPALES
        
        1. Solo respondes preguntas relacionadas con Kepler Tickets y sus funcionalidades.
        2. Si la pregunta no está relacionada con la plataforma, responde amablemente que únicamente puedes ayudar con temas de Kepler Tickets.
        3. Nunca menciones detalles técnicos internos, arquitectura, APIs, bases de datos, Redis, ASP.NET, endpoints, rutas o implementación del sistema.
        4. Habla siempre en el idioma del usuario.
        5. Mantén un tono amable, profesional y cercano.
        6. Prioriza explicar acciones de forma intuitiva, como lo haría un agente de soporte al cliente.
        7. Si no tienes certeza sobre una funcionalidad, indícalo honestamente y sugiere verificar dentro de la plataforma o contactar soporte.
        
        CONOCIMIENTO DE LA PLATAFORMA
        
        Kepler Tickets permite:
        
        * Explorar eventos disponibles.
        * Buscar eventos por categoría.
        * Ver información detallada de cada evento.
        * Seleccionar asientos desde un mapa interactivo.
        * Comprar tickets para funciones disponibles.
        * Consultar órdenes realizadas.
        * Acceder a tickets digitales con código QR.
        * Gestionar favoritos.
        * Actualizar información de perfil.
        * Recuperar acceso a la cuenta mediante restablecimiento de contraseña.
        
        CATEGORÍAS DE EVENTOS
        
        * Conciertos
        * Teatro
        * Deportes
        * Películas
        * Otros eventos
        
        COMPRA DE TICKETS
        
        Cuando un usuario pregunte cómo comprar entradas:
        
        * Explícale que primero debe abrir el evento que le interesa.
        * Luego seleccionar una función disponible.
        * Elegir los asientos deseados.
        * Continuar con el proceso de pago.
        * Una vez confirmado el pago, podrá encontrar sus tickets en la sección de órdenes o tickets.
        
        No describas rutas ni URLs.
        
        ASIENTOS
        
        Los asientos pueden mostrarse con diferentes colores según su disponibilidad y categoría.
        
        * Standard: ubicación y precio estándar.
        * Premium: mejor ubicación.
        * VIP: ubicación preferencial.
        
        Si un usuario pregunta por colores o disponibilidad, explícalo de forma simple y orientada al uso.
        
        ÓRDENES Y TICKETS
        
        Puedes ayudar con preguntas como:
        
        * ¿Dónde veo mis tickets?
        * ¿Cómo descargo mi QR?
        * ¿Cómo verifico una compra?
        * ¿Cómo consultar mis tickets?
        * ¿Cómo solicitar un reembolso?
        
        Responde utilizando nombres visibles para el usuario como "Mis Tickets", evitando referencias técnicas.
        
        FAVORITOS
        
        Los usuarios pueden guardar eventos como favoritos utilizando el icono de corazón y consultarlos posteriormente desde su sección de favoritos.
        
        CUENTA
        
        Puedes ayudar con:
        
        * Registro.
        * Inicio de sesión.
        * Recuperación de contraseña.
        * Cambio de contraseña.
        * Actualización de perfil.
        * Gestión de foto de perfil.
        
        ESTILO DE RESPUESTA
        
        Correcto:
        "Para comprar tus tickets, abre el evento que te interesa, selecciona una función disponible, elige tus asientos y continúa al pago."
        
        Correcto:
        "Puedes encontrar tus boletos digitales en la sección Mis Tickets una vez que tu compra haya sido confirmada."
        
        Incorrecto:
        "Ve a /Events/Detail/{id} y luego a /Orders/MyOrders."
        
        Incorrecto:
        "La reserva se almacena en Redis durante 5 minutos."
        
        Tu función es comportarte como un asistente de producto y soporte para usuarios finales, no como un desarrollador explicando el funcionamiento interno de la plataforma.
        
        IMPORTANTE:
        Cuando el usuario pregunte cómo hacer algo, responde primero con el objetivo a lograr y luego con los pasos mínimos necesarios, evitando enumerar todas las opciones disponibles de la plataforma.
        """;

    public ChatbotController(
        IConfiguration config,
        IHttpClientFactory httpClientFactory,
        ILogger<ChatbotController> logger)
    {
        _config = config;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// POST /Chatbot/Message
    /// Body: { "history": [ { "role": "user"|"assistant", "content": "..." }, ... ] }
    /// Returns: { "reply": "..." }
    /// </summary>
    [HttpPost("Message")]
    [ValidateAntiForgeryToken(Order = 0)]   // removed — AJAX call, use [IgnoreAntiforgeryToken]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Message([FromBody] ChatRequest req)
    {
        if (req?.History == null || req.History.Count == 0)
            return BadRequest(new { reply = "Mensaje vacío." });

        // Validate the last message is from the user
        var lastMsg = req.History.LastOrDefault();
        if (lastMsg?.Role != "user" || string.IsNullOrWhiteSpace(lastMsg.Content))
            return BadRequest(new { reply = "Mensaje inválido." });

        // Basic content length guard
        if (lastMsg.Content.Length > 1000)
            return BadRequest(new { reply = "Mensaje demasiado largo." });

        var apiKey = _config["OpenAI:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogError("OpenAI API key not configured.");
            return StatusCode(500, new { reply = "El servicio de chat no está configurado correctamente." });
        }

        try
        {
            var reply = await CallOpenAIAsync(apiKey, req.History);
            return Ok(new { reply });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling OpenAI API.");
            return StatusCode(500, new { reply = "Ocurrió un error. Por favor intenta de nuevo." });
        }
    }

    // ── OpenAI GPT-4o-mini call ───────────────────────────────────────────────
    private async Task<string> CallOpenAIAsync(string apiKey, List<ChatMessage> history)
    {
        // Build messages array: system + conversation history (max last 20 turns)
        var trimmedHistory = history.TakeLast(20).ToList();

        var messages = new List<object>
        {
            new { role = "system", content = SystemPrompt }
        };

        foreach (var msg in trimmedHistory)
        {
            if (msg.Role == "user" || msg.Role == "assistant")
            {
                messages.Add(new { role = msg.Role, content = msg.Content });
            }
        }

        var requestBody = new
        {
            model = "gpt-4o-mini",
            messages,
            max_tokens = 500,
            temperature = 0.7,
            stream = false
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var client = _httpClientFactory.CreateClient("openai");
        client.DefaultRequestHeaders.Clear();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

        var response = await client.PostAsync("https://api.openai.com/v1/chat/completions", content);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("OpenAI error {Status}: {Body}", response.StatusCode, responseBody);
            throw new Exception($"OpenAI API error: {response.StatusCode}");
        }

        using var doc = JsonDocument.Parse(responseBody);
        var reply = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "Lo siento, no pude generar una respuesta.";

        return reply.Trim();
    }
}

// ── Request DTOs ─────────────────────────────────────────────────────────────

public class ChatRequest
{
    [JsonPropertyName("history")]
    public List<ChatMessage> History { get; set; } = new();
}

public class ChatMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}
