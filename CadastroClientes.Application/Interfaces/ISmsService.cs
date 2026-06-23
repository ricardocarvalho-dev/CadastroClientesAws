using CadastroClientes.Application.DTOs;

namespace CadastroClientes.Application.Interfaces;

public interface ISmsService
{
    Task<SmsResultadoDto> EnviarAsync(string celular, string nomeCliente, string mensagem);
}