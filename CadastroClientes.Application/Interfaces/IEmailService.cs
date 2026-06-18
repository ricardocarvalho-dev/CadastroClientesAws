using CadastroClientes.Application.DTOs;

namespace CadastroClientes.Application.Interfaces;

public interface IEmailService
{
    Task<EmailResultadoDto> EnviarAsync(string destinatarioEmail, string nomeCliente, string mensagem);
}