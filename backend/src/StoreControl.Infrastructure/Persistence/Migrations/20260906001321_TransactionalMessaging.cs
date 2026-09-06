using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StoreControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TransactionalMessaging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "integration");

            migrationBuilder.CreateTable(
                name: "InboxMessages",
                schema: "integration",
                columns: table => new
                {
                    Consumer = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayloadHash = table.Column<byte[]>(type: "binary(32)", fixedLength: true, maxLength: 32, nullable: false),
                    ProcessedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboxMessages", x => new { x.Consumer, x.MessageId });
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                schema: "integration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    RoutingKey = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    NextAttemptAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    PublishAttempts = table.Column<int>(type: "int", nullable: false),
                    LockId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LockedUntilUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                    table.CheckConstraint("CK_OutboxMessages_Lease", "([LockId] IS NULL AND [LockedUntilUtc] IS NULL) OR ([LockId] IS NOT NULL AND [LockedUntilUtc] IS NOT NULL)");
                    table.CheckConstraint("CK_OutboxMessages_Payload_Json", "ISJSON([Payload]) = 1");
                    table.CheckConstraint("CK_OutboxMessages_PublishAttempts", "[PublishAttempts] >= 0");
                });

            migrationBuilder.CreateTable(
                name: "SaleReceiptDeliveries",
                schema: "integration",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SaleId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RecipientHash = table.Column<byte[]>(type: "binary(32)", fixedLength: true, maxLength: 32, nullable: true),
                    ProcessedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaleReceiptDeliveries", x => x.Id);
                    table.CheckConstraint("CK_SaleReceiptDeliveries_Recipient", "([Status] = 1 AND [RecipientHash] IS NOT NULL) OR ([Status] = 2 AND [RecipientHash] IS NULL)");
                    table.CheckConstraint("CK_SaleReceiptDeliveries_Status", "[Status] IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_SaleReceiptDeliveries_Sales_SaleId",
                        column: x => x.SaleId,
                        principalSchema: "sales",
                        principalTable: "Sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessages_ProcessedAtUtc",
                schema: "integration",
                table: "InboxMessages",
                column: "ProcessedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_PublishedAtUtc_NextAttemptAtUtc_LockedUntilUtc",
                schema: "integration",
                table: "OutboxMessages",
                columns: new[] { "PublishedAtUtc", "NextAttemptAtUtc", "LockedUntilUtc" },
                filter: "[PublishedAtUtc] IS NULL")
                .Annotation("SqlServer:Include", new[] { "CreatedAtUtc", "PublishAttempts" });

            migrationBuilder.CreateIndex(
                name: "IX_SaleReceiptDeliveries_MessageId",
                schema: "integration",
                table: "SaleReceiptDeliveries",
                column: "MessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SaleReceiptDeliveries_SaleId",
                schema: "integration",
                table: "SaleReceiptDeliveries",
                column: "SaleId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InboxMessages",
                schema: "integration");

            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "integration");

            migrationBuilder.DropTable(
                name: "SaleReceiptDeliveries",
                schema: "integration");
        }
    }
}
