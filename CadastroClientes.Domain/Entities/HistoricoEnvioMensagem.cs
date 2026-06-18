namespace CadastroClientes.Domain.Entities;

public class HistoricoEnvioMensagem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClienteId { get; set; }
    public required string Canal { get; set; }          // "Email" ou "SMS"
    public required string Destinatario { get; set; }    // e-mail ou número de celular
    public required string Status { get; set; }          // "Sucesso" ou "Falha"
    public string? MensagemErro { get; set; }
    public string? ProviderMessageId { get; set; }        // ID retornado pelo Resend
    public DateTime DataEnvio { get; set; } = DateTime.UtcNow;
}