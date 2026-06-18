using CadastroClientes.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CadastroClientes.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public DbSet<Cliente> Clientes { get; set; }
    public DbSet<HistoricoEnvioMensagem> HistoricosEnvioMensagem { get; set; }

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configurações da entidade Cliente
        modelBuilder.Entity<Cliente>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .ValueGeneratedNever();

            entity.Property(e => e.Nome)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(e => e.Email)
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(e => e.Celular)
                .IsRequired()
                .HasMaxLength(20);

            entity.Property(e => e.DataCadastro)
                .ValueGeneratedOnAdd();

            // Índice único para Email
            entity.HasIndex(e => e.Email)
                .IsUnique();
        });

        // Configurações da entidade HistoricoEnvioMensagem
        modelBuilder.Entity<HistoricoEnvioMensagem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id)
                .ValueGeneratedNever();

            entity.Property(e => e.Canal)
                .IsRequired()
                .HasMaxLength(20);

            entity.Property(e => e.Destinatario)
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(e => e.Status)
                .IsRequired()
                .HasMaxLength(20);

            entity.Property(e => e.MensagemErro)
                .HasMaxLength(1000);

            entity.Property(e => e.ProviderMessageId)
                .HasMaxLength(100);

            entity.Property(e => e.DataEnvio)
                .ValueGeneratedOnAdd();

            // Índice para consultas rápidas por cliente
            entity.HasIndex(e => e.ClienteId);
        });
    }
}
