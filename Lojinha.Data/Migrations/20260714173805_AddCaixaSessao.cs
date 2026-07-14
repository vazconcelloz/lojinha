using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lojinha.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCaixaSessao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CaixaSessoes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DataAbertura = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ValorAbertura = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    UsuarioAbertura = table.Column<string>(type: "TEXT", nullable: false),
                    DataFechamento = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ValorContado = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: true),
                    ValorEsperado = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: true),
                    Diferenca = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: true),
                    UsuarioFechamento = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaixaSessoes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MovimentosCaixa",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CaixaSessaoId = table.Column<int>(type: "INTEGER", nullable: false),
                    Tipo = table.Column<int>(type: "INTEGER", nullable: false),
                    Valor = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    DataHora = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AutorizadoPor = table.Column<string>(type: "TEXT", nullable: false),
                    Observacao = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimentosCaixa", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MovimentosCaixa_CaixaSessoes_CaixaSessaoId",
                        column: x => x.CaixaSessaoId,
                        principalTable: "CaixaSessoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MovimentosCaixa_CaixaSessaoId",
                table: "MovimentosCaixa",
                column: "CaixaSessaoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MovimentosCaixa");

            migrationBuilder.DropTable(
                name: "CaixaSessoes");
        }
    }
}
