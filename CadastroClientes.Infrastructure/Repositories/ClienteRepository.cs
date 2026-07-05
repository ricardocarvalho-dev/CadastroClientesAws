using CadastroClientes.Application.Interfaces;
using CadastroClientes.Domain.Entities;
using CadastroClientes.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CadastroClientes.Infrastructure.Repositories;

public class ClienteRepository : IClienteRepository
{
    private readonly AppDbContext _context;

    public ClienteRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Cliente> CriarAsync(Cliente cliente)
    {
        try
        {
            _context.Clientes.Add(cliente);
            await _context.SaveChangesAsync();
            return cliente;
        }
        catch (DbUpdateException ex) when (IsDuplicatePrimaryKeyError(ex))
        {
            // O EnableRetryOnFailure do EF Core pode reenviar o mesmo INSERT
            // (mesmo Id, gerado uma única vez em memória na entidade Cliente)
            // quando a confirmação de rede do primeiro INSERT demora ou se perde,
            // mesmo que o Postgres já tenha persistido o registro com sucesso.
            // Nesse caso, tratamos como sucesso em vez de propagar o erro.
            var existente = await _context.Clientes
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == cliente.Id);

            if (existente is not null)
                return existente;

            // Não achou o registro -> não foi retry, é duplicidade real de outra causa.
            throw;
        }
    }

    public async Task<IEnumerable<Cliente>> ListarTodosAsync()
    {
        return await _context.Clientes
            .OrderByDescending(c => c.DataCadastro)
            .ToListAsync();
    }

    public async Task<Cliente?> ObterPorIdAsync(Guid id)
    {
        return await _context.Clientes.FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<Cliente?> ObterPorEmailAsync(string email)
    {
        return await _context.Clientes.FirstOrDefaultAsync(c => c.Email == email);
    }

    public async Task<bool> EmailJaExisteAsync(string email)
    {
        return await _context.Clientes.AnyAsync(c => c.Email == email);
    }

    public async Task<Cliente> AtualizarAsync(Cliente cliente)
    {
        _context.Clientes.Update(cliente);
        await _context.SaveChangesAsync();
        return cliente;
    }

    public async Task ExcluirAsync(Cliente cliente)
    {
        _context.Clientes.Remove(cliente);
        await _context.SaveChangesAsync();
    }

    private static bool IsDuplicatePrimaryKeyError(DbUpdateException ex)
    {
        return ex.InnerException is PostgresException pgEx
            && pgEx.SqlState == "23505"
            && pgEx.ConstraintName == "PK_Clientes";
    }
}