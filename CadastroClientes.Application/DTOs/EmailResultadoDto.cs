namespace CadastroClientes.Application.DTOs;

public class EmailResultadoDto
{
    public bool Sucesso { get; set; }
    public string? ProviderMessageId { get; set; }
    public string? MensagemErro { get; set; }
}