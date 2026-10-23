using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthTrack.Migrations
{
    /// <inheritdoc />
    public partial class RemoveHealthProfessionalRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Keep any existing health professionals as trainers, so their appointments and reviews stay valid.
            migrationBuilder.Sql("""
                INSERT INTO [TrainerProfiles] ([UserId], [Specialization], [YearsOfExperience], [UpdatedAt])
                SELECT p.[UserId],
                       CASE p.[Profession] WHEN N'Dietitian or nutritionist' THEN N'Nutrition and diet'
                                           WHEN N'Physiotherapist' THEN N'Injury recovery'
                                           ELSE N'General fitness' END,
                       p.[YearsOfExperience], SYSUTCDATETIME()
                FROM [ProfessionalProfiles] p
                WHERE NOT EXISTS (SELECT 1 FROM [TrainerProfiles] t WHERE t.[UserId] = p.[UserId]);

                INSERT INTO [TrainerQualifications] ([TrainerProfileId], [Name])
                SELECT t.[Id], LEFT(p.[Profession] + N' (reg. ' + p.[RegistrationNumber] + N')', 100)
                FROM [ProfessionalProfiles] p JOIN [TrainerProfiles] t ON t.[UserId] = p.[UserId];

                UPDATE ur SET [RoleId] = (SELECT [Id] FROM [AspNetRoles] WHERE [Name] = N'Trainer')
                FROM [AspNetUserRoles] ur JOIN [AspNetRoles] r ON r.[Id] = ur.[RoleId]
                WHERE r.[Name] = N'Professional' AND EXISTS (SELECT 1 FROM [AspNetRoles] WHERE [Name] = N'Trainer');

                DELETE FROM [AspNetUserRoles] WHERE [RoleId] IN (SELECT [Id] FROM [AspNetRoles] WHERE [Name] = N'Professional');
                DELETE FROM [AspNetRoleClaims] WHERE [RoleId] IN (SELECT [Id] FROM [AspNetRoles] WHERE [Name] = N'Professional');
                DELETE FROM [AspNetRoles] WHERE [Name] = N'Professional';

                DELETE FROM [Notifications] WHERE [Type] = N'ProfessionalChosen';
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_AspNetUsers_ProfessionalId",
                table: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "ProfessionalProfiles");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_ProfessionalId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ProfessionalId",
                table: "AspNetUsers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProfessionalId",
                table: "AspNetUsers",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProfessionalProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Profession = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RegistrationNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Workplace = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    YearsOfExperience = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfessionalProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProfessionalProfiles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_ProfessionalId",
                table: "AspNetUsers",
                column: "ProfessionalId");

            migrationBuilder.CreateIndex(
                name: "IX_ProfessionalProfiles_UserId",
                table: "ProfessionalProfiles",
                column: "UserId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_AspNetUsers_ProfessionalId",
                table: "AspNetUsers",
                column: "ProfessionalId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }
    }
}
