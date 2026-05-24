using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Remotely.Server.Migrations.Sqlite
{
    /// <inheritdoc />
    /// <remarks>
    /// Multi-Tenant Membership migration (spec FR-19, §6.2).
    ///
    /// Order matters: structural changes that do NOT depend on the legacy columns
    /// come first, then a raw-SQL data copy from RemotelyUsers.(OrganizationID,
    /// IsAdministrator) into UserOrganizationMemberships, then the legacy column
    /// drops. The unscaffolded ordering would have dropped the columns first and
    /// lost the data.
    ///
    /// InviteLinks rows are deleted as part of this migration (intentional
    /// truncation per FR-19 exception clause / KD-04). Existing unclaimed
    /// invitations are not forward-compatible with the base-privileges-only
    /// invite flow.
    ///
    /// SQLite-only per CT-06 / OI-02. The SqlServer and PostgreSql migration
    /// folders are intentionally NOT updated; those backends are deferred.
    /// </remarks>
    public partial class MultiTenantMembership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ----------------------------------------------------------------
            // Step 1: Additive schema changes (new table, ApiTokens.CreatorId).
            // ----------------------------------------------------------------

            migrationBuilder.AddColumn<string>(
                name: "CreatorId",
                table: "ApiTokens",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UserOrganizationMemberships",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    OrganizationId = table.Column<string>(type: "TEXT", nullable: false),
                    IsAdministrator = table.Column<bool>(type: "INTEGER", nullable: false),
                    CanInvite = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserOrganizationMemberships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserOrganizationMemberships_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserOrganizationMemberships_RemotelyUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "RemotelyUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApiTokens_CreatorId",
                table: "ApiTokens",
                column: "CreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_UserOrganizationMemberships_OrganizationId",
                table: "UserOrganizationMemberships",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_UserOrganizationMemberships_UserId_OrganizationId",
                table: "UserOrganizationMemberships",
                columns: new[] { "UserId", "OrganizationId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ApiTokens_RemotelyUsers_CreatorId",
                table: "ApiTokens",
                column: "CreatorId",
                principalTable: "RemotelyUsers",
                principalColumn: "Id");

            // ----------------------------------------------------------------
            // Step 2: Data migration — backfill UserOrganizationMemberships from
            // the legacy single-org FK on RemotelyUsers (FR-19). Generate a
            // 32-char hex id per row; the unique constraint on (UserId, OrgId)
            // is naturally satisfied since each user had at most one org.
            // ----------------------------------------------------------------
            migrationBuilder.Sql(@"
                INSERT INTO UserOrganizationMemberships (Id, UserId, OrganizationId, IsAdministrator, CanInvite)
                SELECT
                    lower(hex(randomblob(16))),
                    Id,
                    OrganizationID,
                    COALESCE(IsAdministrator, 0),
                    0
                FROM RemotelyUsers
                WHERE OrganizationID IS NOT NULL AND OrganizationID != '';
            ");

            // ----------------------------------------------------------------
            // Step 3: Delete all InviteLink rows (FR-19 exception clause).
            // Unclaimed invitations are not forward-compatible with the new model.
            // ----------------------------------------------------------------
            migrationBuilder.Sql("DELETE FROM InviteLinks;");

            // ----------------------------------------------------------------
            // Step 4: Drop the legacy columns and indexes — safe now that data
            // has been migrated.
            // ----------------------------------------------------------------
            migrationBuilder.DropForeignKey(
                name: "FK_RemotelyUsers_Organizations_OrganizationID",
                table: "RemotelyUsers");

            migrationBuilder.DropIndex(
                name: "IX_RemotelyUsers_OrganizationID",
                table: "RemotelyUsers");

            migrationBuilder.DropColumn(
                name: "IsAdministrator",
                table: "RemotelyUsers");

            migrationBuilder.DropColumn(
                name: "OrganizationID",
                table: "RemotelyUsers");

            migrationBuilder.DropColumn(
                name: "IsAdmin",
                table: "InviteLinks");

            // ----------------------------------------------------------------
            // Step 5: Make ApiTokens.OrganizationID nullable. The legacy FK is
            // recreated as a nullable relationship; the column itself is
            // retained (FR-28 / OI-08) so ApiAuthorizationFilter can detect
            // pre-migration tokens via non-null OrganizationID and 401 them.
            // ----------------------------------------------------------------
            migrationBuilder.DropForeignKey(
                name: "FK_ApiTokens_Organizations_OrganizationID",
                table: "ApiTokens");

            migrationBuilder.AlterColumn<string>(
                name: "OrganizationID",
                table: "ApiTokens",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AddForeignKey(
                name: "FK_ApiTokens_Organizations_OrganizationID",
                table: "ApiTokens",
                column: "OrganizationID",
                principalTable: "Organizations",
                principalColumn: "ID");
        }

        /// <inheritdoc />
        /// <remarks>
        /// Down() restores the schema but cannot restore deleted InviteLink rows
        /// or the original per-user OrganizationID / IsAdministrator values.
        /// Applying Down on a database that ran Up will result in empty
        /// OrganizationID / IsAdministrator columns and no invitations. This is
        /// inherent to the data-truncating nature of the migration; the spec
        /// (FR-19) does not require a non-destructive roll-back.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ApiTokens_Organizations_OrganizationID",
                table: "ApiTokens");

            migrationBuilder.DropForeignKey(
                name: "FK_ApiTokens_RemotelyUsers_CreatorId",
                table: "ApiTokens");

            migrationBuilder.DropTable(
                name: "UserOrganizationMemberships");

            migrationBuilder.DropIndex(
                name: "IX_ApiTokens_CreatorId",
                table: "ApiTokens");

            migrationBuilder.DropColumn(
                name: "CreatorId",
                table: "ApiTokens");

            migrationBuilder.AddColumn<bool>(
                name: "IsAdministrator",
                table: "RemotelyUsers",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OrganizationID",
                table: "RemotelyUsers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAdmin",
                table: "InviteLinks",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "OrganizationID",
                table: "ApiTokens",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RemotelyUsers_OrganizationID",
                table: "RemotelyUsers",
                column: "OrganizationID");

            migrationBuilder.AddForeignKey(
                name: "FK_ApiTokens_Organizations_OrganizationID",
                table: "ApiTokens",
                column: "OrganizationID",
                principalTable: "Organizations",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RemotelyUsers_Organizations_OrganizationID",
                table: "RemotelyUsers",
                column: "OrganizationID",
                principalTable: "Organizations",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
