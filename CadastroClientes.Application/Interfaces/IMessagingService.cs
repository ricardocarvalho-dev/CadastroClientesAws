namespace CadastroClientes.Application.Interfaces;

public interface IMessagingService
{
    Task PublicarCriacaoClienteAsync(Guid clienteId, string nome, string email, string celular, string mensagem, string canal = "Email");
}
