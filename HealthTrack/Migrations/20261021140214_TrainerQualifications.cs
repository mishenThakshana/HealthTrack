using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthTrack.Migrations
{
    /// <inheritdoc />
    public partial class TrainerQualifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TrainerQualifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TrainerProfileId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainerQualifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrainerQualifications_TrainerProfiles_TrainerProfileId",
                        column: x => x.TrainerProfileId,
                        principalTable: "TrainerProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TrainerQualifications_TrainerProfileId",
                table: "TrainerQualifications",
                column: "TrainerProfileId");

            // Keep existing data: each comma-separated part of the old text becomes its own qualification.
            migrationBuilder.Sql("""
                INSERT INTO TrainerQualifications (TrainerProfileId, Name)
                SELECT p.Id, LEFT(LTRIM(RTRIM(q.value)), 100)
                FROM TrainerProfiles p
                CROSS APPLY STRING_SPLIT(p.Certifications, ',', 1) q
                WHERE LTRIM(RTRIM(q.value)) <> ''
                ORDER BY p.Id, q.ordinal;
                """);

            migrationBuilder.DropColumn(
                name: "Certifications",
                table: "TrainerProfiles");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Certifications",
                table: "TrainerProfiles",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE p SET Certifications = LEFT(ISNULL((
                    SELECT STRING_AGG(q.Name, ', ') WITHIN GROUP (ORDER BY q.Id)
                    FROM TrainerQualifications q WHERE q.TrainerProfileId = p.Id), ''), 200)
                FROM TrainerProfiles p;
                """);

            migrationBuilder.DropTable(
                name: "TrainerQualifications");
        }
    }
}
