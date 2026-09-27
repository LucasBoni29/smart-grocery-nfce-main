using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartGrocery.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddNfceAccessKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NfceAccessKey",
                table: "Purchases",
                type: "character varying(44)",
                maxLength: 44,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Purchases_NfceAccessKey",
                table: "Purchases",
                column: "NfceAccessKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Purchases_NfceAccessKey",
                table: "Purchases");

            migrationBuilder.DropColumn(
                name: "NfceAccessKey",
                table: "Purchases");
        }
    }
}
