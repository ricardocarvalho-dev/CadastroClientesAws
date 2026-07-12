using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CadastroClientes.Application.DTOs;
using CadastroClientes.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CadastroClientes.Infrastructure.Email;

public class ResendEmailService : IEmailService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ResendEmailService> _logger;
    private readonly string _apiKey;
    private readonly string _fromEmail;
    private readonly string _fromName;

    public ResendEmailService(HttpClient httpClient, IConfiguration configuration, ILogger<ResendEmailService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = configuration["Resend:ApiKey"] ?? throw new InvalidOperationException("Resend:ApiKey não configurada");
        _fromEmail = configuration["Resend:FromEmail"] ?? "noreply@evoluasoftware.com.br";
        _fromName = configuration["Resend:FromName"] ?? "Ricardodev Solu\u00e7\u00e3o WEB";
    }

    public async Task<EmailResultadoDto> EnviarAsync(string destinatarioEmail, string nomeCliente, string mensagem)
    {
        var payload = new
        {
            from = $"{_fromName} <{_fromEmail}>",
            to = new[] { destinatarioEmail },
            subject = "Recebemos sua mensagem",
            html = $"<p>Olá, {nomeCliente}!</p><p>Recebemos o seu cadastro com a seguinte mensagem:</p><blockquote>{mensagem}</blockquote><p>Em breve entraremos em contato.</p>"
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        try
        {
            var response = await _httpClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(responseBody);
                var id = doc.RootElement.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;

                return new EmailResultadoDto
                {
                    Sucesso = true,
                    ProviderMessageId = id
                };
            }

            _logger.LogWarning($"Resend retornou status {(int)response.StatusCode}: {responseBody}");
            return new EmailResultadoDto
            {
                Sucesso = false,
                MensagemErro = $"Resend respondeu {(int)response.StatusCode}: {responseBody}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"Erro ao chamar a API do Resend: {ex.Message}");
            return new EmailResultadoDto
            {
                Sucesso = false,
                MensagemErro = ex.Message
            };
        }
    }
}
