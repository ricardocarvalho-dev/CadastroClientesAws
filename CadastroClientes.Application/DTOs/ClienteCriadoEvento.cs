namespace CadastroClientes.Application.DTOs;

public class ClienteCriadoEvento
{
    public Guid ClienteId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Celular { get; set; } = string.Empty;
    public string Mensagem { get; set; } = string.Empty;
    public DateTime DataCadastro { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string Canal { get; set; } = "Email";
}