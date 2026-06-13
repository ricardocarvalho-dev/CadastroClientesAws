using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CadastroClientes.Infrastructure.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Clientes",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Nome = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                Celular = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                Email = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                DataCadastro = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Clientes", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Clientes_Email",
            table: "Clientes",
            column: "Email",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Clientes");
    }
}
