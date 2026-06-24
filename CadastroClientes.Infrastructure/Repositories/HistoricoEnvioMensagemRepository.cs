using CadastroClientes.Application.Interfaces;
using CadastroClientes.Domain.Entities;
using CadastroClientes.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CadastroClientes.Infrastructure.Repositories;

public class HistoricoEnvioMensagemRepository : IHistoricoEnvioMensagemRepository
{
    private readonly AppDbContext _context;

    public HistoricoEnvioMensagemRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<HistoricoEnvioMensagem> RegistrarAsync(HistoricoEnvioMensagem historico)
    {
        _context.HistoricosEnvioMensagem.Add(historico);
        await _context.SaveChangesAsync();
        return historico;
    }

    public async Task<bool> JaEnviadoComSucessoAsync(Guid clienteId, string canal)
    {
        return await _context.HistoricosEnvioMensagem
            .AnyAsync(h => h.ClienteId == clienteId && h.Canal == canal && h.Status == "Sucesso");
    }

    public async Task<IEnumerable<HistoricoEnvioMensagem>> ListarAsync()
    {
        return await _context.HistoricosEnvioMensagem
            .OrderByDescending(h => h.DataEnvio)
            .ToListAsync();
    }
}