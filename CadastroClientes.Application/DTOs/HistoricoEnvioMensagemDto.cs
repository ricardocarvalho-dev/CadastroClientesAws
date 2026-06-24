namespace CadastroClientes.Application.DTOs;

public class HistoricoEnvioMensagemDto
{
    public Guid Id { get; set; }
    public Guid ClienteId { get; set; }
    public string Canal { get; set; } = string.Empty;
    public string Destinatario { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? MensagemErro { get; set; }
    public string? ProviderMessageId { get; set; }
    public DateTime DataEnvio { get; set; }
}