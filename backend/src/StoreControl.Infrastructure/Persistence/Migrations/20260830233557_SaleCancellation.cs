using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StoreControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SaleCancellation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Sales_Status_Finalized",
                schema: "sales",
                table: "Sales");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CashMovements_SalePayment",
                schema: "cash",
                table: "CashMovements");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CashMovements_TypeDirection",
                schema: "cash",
                table: "CashMovements");

            migrationBuilder.AddColumn<long>(
                name: "CancellationCashSessionId",
                schema: "sales",
                table: "Sales",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CancellationOperationId",
                schema: "sales",
                table: "Sales",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                schema: "sales",
                table: "Sales",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAtUtc",
                schema: "sales",
                table: "Sales",
                type: "datetime2(3)",
                precision: 3,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CancelledByUserId",
                schema: "sales",
                table: "Sales",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                schema: "sales",
                table: "Sales",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<long>(
                name: "ReversedSalePaymentId",
                schema: "cash",
                table: "CashMovements",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SaleStatusHistory",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SaleId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaleStatusHistory", x => x.Id);
                    table.CheckConstraint("CK_SaleStatusHistory_Description_NotBlank", "LEN(LTRIM(RTRIM([Description]))) > 0");
                    table.CheckConstraint("CK_SaleStatusHistory_Status", "[Status] IN (2, 3)");
                    table.ForeignKey(
                        name: "FK_SaleStatusHistory_Sales_SaleId",
                        column: x => x.SaleId,
                        principalSchema: "sales",
                        principalTable: "Sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SaleStatusHistory_Users_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalSchema: "security",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql("""
                INSERT INTO [sales].[SaleStatusHistory]
                    ([SaleId], [Status], [ChangedByUserId], [ChangedAtUtc], [Description])
                SELECT [Id], 2, [CreatedByUserId], [FinalizedAtUtc], N'Venda finalizada.'
                FROM [sales].[Sales];
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Sales_CancellationCashSessionId",
                schema: "sales",
                table: "Sales",
                column: "CancellationCashSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_CancellationOperationId",
                schema: "sales",
                table: "Sales",
                column: "CancellationOperationId",
                unique: true,
                filter: "[CancellationOperationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_CancelledByUserId",
                schema: "sales",
                table: "Sales",
                column: "CancelledByUserId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Sales_CancellationLifecycle",
                schema: "sales",
                table: "Sales",
                sql: "([Status] = 2 AND [CancellationOperationId] IS NULL AND [CancellationCashSessionId] IS NULL AND [CancelledByUserId] IS NULL AND [CancelledAtUtc] IS NULL AND [CancellationReason] IS NULL) OR ([Status] = 3 AND [CancellationOperationId] IS NOT NULL AND [CancellationCashSessionId] IS NOT NULL AND [CancelledByUserId] IS NOT NULL AND [CancelledAtUtc] IS NOT NULL AND LEN(LTRIM(RTRIM([CancellationReason]))) > 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Sales_Status",
                schema: "sales",
                table: "Sales",
                sql: "[Status] IN (2, 3)");

            migrationBuilder.CreateIndex(
                name: "IX_CashMovements_ReversedSalePaymentId",
                schema: "cash",
                table: "CashMovements",
                column: "ReversedSalePaymentId",
                unique: true,
                filter: "[ReversedSalePaymentId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CashMovements_SalePayment",
                schema: "cash",
                table: "CashMovements",
                sql: "([MovementType] = 2 AND [PaymentMethod] BETWEEN 1 AND 6 AND [SalePaymentId] IS NOT NULL AND [ReversedSalePaymentId] IS NULL) OR ([MovementType] = 5 AND [PaymentMethod] BETWEEN 1 AND 6 AND [SalePaymentId] IS NULL AND [ReversedSalePaymentId] IS NOT NULL) OR ([MovementType] NOT IN (2, 5) AND [PaymentMethod] IS NULL AND [SalePaymentId] IS NULL AND [ReversedSalePaymentId] IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CashMovements_TypeDirection",
                schema: "cash",
                table: "CashMovements",
                sql: "([MovementType] IN (1, 2, 3) AND [Amount] > 0) OR ([MovementType] IN (4, 5) AND [Amount] < 0)");

            migrationBuilder.CreateIndex(
                name: "IX_SaleStatusHistory_ChangedByUserId",
                schema: "sales",
                table: "SaleStatusHistory",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleStatusHistory_SaleId_ChangedAtUtc",
                schema: "sales",
                table: "SaleStatusHistory",
                columns: new[] { "SaleId", "ChangedAtUtc" });

            migrationBuilder.AddForeignKey(
                name: "FK_CashMovements_SalePayments_ReversedSalePaymentId",
                schema: "cash",
                table: "CashMovements",
                column: "ReversedSalePaymentId",
                principalSchema: "sales",
                principalTable: "SalePayments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Sales_CashSessions_CancellationCashSessionId",
                schema: "sales",
                table: "Sales",
                column: "CancellationCashSessionId",
                principalSchema: "cash",
                principalTable: "CashSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Sales_Users_CancelledByUserId",
                schema: "sales",
                table: "Sales",
                column: "CancelledByUserId",
                principalSchema: "security",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CashMovements_SalePayments_ReversedSalePaymentId",
                schema: "cash",
                table: "CashMovements");

            migrationBuilder.DropForeignKey(
                name: "FK_Sales_CashSessions_CancellationCashSessionId",
                schema: "sales",
                table: "Sales");

            migrationBuilder.DropForeignKey(
                name: "FK_Sales_Users_CancelledByUserId",
                schema: "sales",
                table: "Sales");

            migrationBuilder.DropTable(
                name: "SaleStatusHistory",
                schema: "sales");

            migrationBuilder.DropIndex(
                name: "IX_Sales_CancellationCashSessionId",
                schema: "sales",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_Sales_CancellationOperationId",
                schema: "sales",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_Sales_CancelledByUserId",
                schema: "sales",
                table: "Sales");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Sales_CancellationLifecycle",
                schema: "sales",
                table: "Sales");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Sales_Status",
                schema: "sales",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_CashMovements_ReversedSalePaymentId",
                schema: "cash",
                table: "CashMovements");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CashMovements_SalePayment",
                schema: "cash",
                table: "CashMovements");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CashMovements_TypeDirection",
                schema: "cash",
                table: "CashMovements");

            migrationBuilder.DropColumn(
                name: "CancellationCashSessionId",
                schema: "sales",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "CancellationOperationId",
                schema: "sales",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "CancellationReason",
                schema: "sales",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "CancelledAtUtc",
                schema: "sales",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "CancelledByUserId",
                schema: "sales",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                schema: "sales",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "ReversedSalePaymentId",
                schema: "cash",
                table: "CashMovements");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Sales_Status_Finalized",
                schema: "sales",
                table: "Sales",
                sql: "[Status] = 2");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CashMovements_SalePayment",
                schema: "cash",
                table: "CashMovements",
                sql: "([MovementType] = 2 AND [PaymentMethod] BETWEEN 1 AND 6 AND [SalePaymentId] IS NOT NULL) OR ([MovementType] <> 2 AND [PaymentMethod] IS NULL AND [SalePaymentId] IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CashMovements_TypeDirection",
                schema: "cash",
                table: "CashMovements",
                sql: "([MovementType] IN (1, 2, 3) AND [Amount] > 0) OR ([MovementType] = 4 AND [Amount] < 0)");
        }
    }
}
