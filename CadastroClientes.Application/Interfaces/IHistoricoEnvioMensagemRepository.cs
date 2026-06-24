using CadastroClientes.Domain.Entities;

namespace CadastroClientes.Application.Interfaces;

public interface IHistoricoEnvioMensagemRepository
{
    Task<HistoricoEnvioMensagem> RegistrarAsync(HistoricoEnvioMensagem historico);
    Task<bool> JaEnviadoComSucessoAsync(Guid clienteId, string canal);
    Task<IEnumerable<HistoricoEnvioMensagem>> ListarAsync();
}
