namespace CadastroClientes.Application.DTOs;

public class CriarClienteDto
{
    public required string Nome { get; set; }
    public required string Celular { get; set; }
    public required string Email { get; set; }
    public string Mensagem { get; set; } = string.Empty;
    public string Canal { get; set; } = "Email"; 
}