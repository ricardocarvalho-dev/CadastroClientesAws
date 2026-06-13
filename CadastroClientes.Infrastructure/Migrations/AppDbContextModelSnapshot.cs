using CadastroClientes.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace CadastroClientes.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
partial class AppDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        modelBuilder.HasAnnotation("ProductVersion", "8.0.0");

        modelBuilder.Entity("CadastroClientes.Domain.Entities.Cliente", b =>
            {
                b.Property<Guid>("Id")
                    .HasColumnType("TEXT");

                b.Property<string>("Celular")
                    .IsRequired()
                    .HasMaxLength(20)
                    .HasColumnType("TEXT");

                b.Property<DateTime>("DataCadastro")
                    .HasColumnType("TEXT");

                b.Property<string>("Email")
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnType("TEXT");

                b.Property<string>("Nome")
                    .IsRequired()
                    .HasMaxLength(150)
                    .HasColumnType("TEXT");

                b.HasKey("Id");

                b.HasIndex("Email")
                    .IsUnique();

                b.ToTable("Clientes");
            });
#pragma warning restore 612, 618
    }
}
