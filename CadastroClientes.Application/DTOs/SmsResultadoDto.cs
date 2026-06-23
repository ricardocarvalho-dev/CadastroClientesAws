namespace CadastroClientes.Application.DTOs;

public class SmsResultadoDto
{
    public bool Sucesso { get; set; }
    public string? ProviderMessageId { get; set; }
    public string? MensagemErro { get; set; }
}