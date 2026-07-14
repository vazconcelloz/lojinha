using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lojinha.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDescontoTroco : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AutorizadoPor",
                table: "Sales",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DescontoValor",
                table: "Sales",
                type: "TEXT",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Troco",
                table: "Sales",
                type: "TEXT",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ValorRecebido",
                table: "Sales",
                type: "TEXT",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DescontoValor",
                table: "SaleItems",
                type: "TEXT",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutorizadoPor",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "DescontoValor",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "Troco",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "ValorRecebido",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "DescontoValor",
                table: "SaleItems");
        }
    }
}
