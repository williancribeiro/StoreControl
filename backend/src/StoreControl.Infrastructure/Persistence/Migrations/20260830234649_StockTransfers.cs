using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StoreControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StockTransfers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StockTransfers",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginStoreId = table.Column<int>(type: "int", nullable: false),
                    DestinationStoreId = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DispatchOperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DispatchedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DispatchedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    ReceiptOperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReceivedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    CancellationOperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CancelledByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockTransfers", x => x.Id);
                    table.CheckConstraint("CK_StockTransfers_DifferentStores", "[OriginStoreId] <> [DestinationStoreId]");
                    table.CheckConstraint("CK_StockTransfers_Lifecycle", "([Status] = 1 AND [DispatchOperationId] IS NULL AND [DispatchedByUserId] IS NULL AND [DispatchedAtUtc] IS NULL AND [ReceiptOperationId] IS NULL AND [ReceivedByUserId] IS NULL AND [ReceivedAtUtc] IS NULL AND [CancellationOperationId] IS NULL AND [CancelledByUserId] IS NULL AND [CancelledAtUtc] IS NULL AND [CancellationReason] IS NULL) OR ([Status] = 2 AND [DispatchOperationId] IS NOT NULL AND [DispatchedByUserId] IS NOT NULL AND [DispatchedAtUtc] IS NOT NULL AND [ReceiptOperationId] IS NULL AND [ReceivedByUserId] IS NULL AND [ReceivedAtUtc] IS NULL AND [CancellationOperationId] IS NULL AND [CancelledByUserId] IS NULL AND [CancelledAtUtc] IS NULL AND [CancellationReason] IS NULL) OR ([Status] = 3 AND [DispatchOperationId] IS NOT NULL AND [DispatchedByUserId] IS NOT NULL AND [DispatchedAtUtc] IS NOT NULL AND [ReceiptOperationId] IS NOT NULL AND [ReceivedByUserId] IS NOT NULL AND [ReceivedAtUtc] IS NOT NULL AND [CancellationOperationId] IS NULL AND [CancelledByUserId] IS NULL AND [CancelledAtUtc] IS NULL AND [CancellationReason] IS NULL) OR ([Status] = 4 AND [DispatchOperationId] IS NULL AND [DispatchedByUserId] IS NULL AND [DispatchedAtUtc] IS NULL AND [ReceiptOperationId] IS NULL AND [ReceivedByUserId] IS NULL AND [ReceivedAtUtc] IS NULL AND [CancellationOperationId] IS NOT NULL AND [CancelledByUserId] IS NOT NULL AND [CancelledAtUtc] IS NOT NULL AND LEN(LTRIM(RTRIM([CancellationReason]))) > 0)");
                    table.CheckConstraint("CK_StockTransfers_Status", "[Status] BETWEEN 1 AND 4");
                    table.ForeignKey(
                        name: "FK_StockTransfers_Stores_DestinationStoreId",
                        column: x => x.DestinationStoreId,
                        principalSchema: "organization",
                        principalTable: "Stores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockTransfers_Stores_OriginStoreId",
                        column: x => x.OriginStoreId,
                        principalSchema: "organization",
                        principalTable: "Stores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockTransfers_Users_CancelledByUserId",
                        column: x => x.CancelledByUserId,
                        principalSchema: "security",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockTransfers_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "security",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockTransfers_Users_DispatchedByUserId",
                        column: x => x.DispatchedByUserId,
                        principalSchema: "security",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockTransfers_Users_ReceivedByUserId",
                        column: x => x.ReceivedByUserId,
                        principalSchema: "security",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockTransferItems",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StockTransferId = table.Column<long>(type: "bigint", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    ProductCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ProductName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockTransferItems", x => x.Id);
                    table.CheckConstraint("CK_StockTransferItems_ProductCode_NotBlank", "LEN(LTRIM(RTRIM([ProductCode]))) > 0");
                    table.CheckConstraint("CK_StockTransferItems_ProductName_NotBlank", "LEN(LTRIM(RTRIM([ProductName]))) > 0");
                    table.CheckConstraint("CK_StockTransferItems_Quantity_Positive", "[Quantity] > 0");
                    table.ForeignKey(
                        name: "FK_StockTransferItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "catalog",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockTransferItems_StockTransfers_StockTransferId",
                        column: x => x.StockTransferId,
                        principalSchema: "inventory",
                        principalTable: "StockTransfers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockTransferStatusHistory",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StockTransferId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockTransferStatusHistory", x => x.Id);
                    table.CheckConstraint("CK_StockTransferStatusHistory_Description_NotBlank", "LEN(LTRIM(RTRIM([Description]))) > 0");
                    table.CheckConstraint("CK_StockTransferStatusHistory_Status", "[Status] BETWEEN 1 AND 4");
                    table.ForeignKey(
                        name: "FK_StockTransferStatusHistory_StockTransfers_StockTransferId",
                        column: x => x.StockTransferId,
                        principalSchema: "inventory",
                        principalTable: "StockTransfers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockTransferStatusHistory_Users_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalSchema: "security",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockTransferItems_ProductId",
                schema: "inventory",
                table: "StockTransferItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransferItems_StockTransferId_ProductId",
                schema: "inventory",
                table: "StockTransferItems",
                columns: new[] { "StockTransferId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_CancellationOperationId",
                schema: "inventory",
                table: "StockTransfers",
                column: "CancellationOperationId",
                unique: true,
                filter: "[CancellationOperationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_CancelledByUserId",
                schema: "inventory",
                table: "StockTransfers",
                column: "CancelledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_CreatedByUserId",
                schema: "inventory",
                table: "StockTransfers",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_DestinationStoreId_CreatedAtUtc",
                schema: "inventory",
                table: "StockTransfers",
                columns: new[] { "DestinationStoreId", "CreatedAtUtc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_DispatchedByUserId",
                schema: "inventory",
                table: "StockTransfers",
                column: "DispatchedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_DispatchOperationId",
                schema: "inventory",
                table: "StockTransfers",
                column: "DispatchOperationId",
                unique: true,
                filter: "[DispatchOperationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_OperationId",
                schema: "inventory",
                table: "StockTransfers",
                column: "OperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_OriginStoreId_CreatedAtUtc",
                schema: "inventory",
                table: "StockTransfers",
                columns: new[] { "OriginStoreId", "CreatedAtUtc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_ReceiptOperationId",
                schema: "inventory",
                table: "StockTransfers",
                column: "ReceiptOperationId",
                unique: true,
                filter: "[ReceiptOperationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_ReceivedByUserId",
                schema: "inventory",
                table: "StockTransfers",
                column: "ReceivedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_Status_CreatedAtUtc",
                schema: "inventory",
                table: "StockTransfers",
                columns: new[] { "Status", "CreatedAtUtc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_StockTransferStatusHistory_ChangedByUserId",
                schema: "inventory",
                table: "StockTransferStatusHistory",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransferStatusHistory_StockTransferId_ChangedAtUtc",
                schema: "inventory",
                table: "StockTransferStatusHistory",
                columns: new[] { "StockTransferId", "ChangedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StockTransferItems",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "StockTransferStatusHistory",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "StockTransfers",
                schema: "inventory");
        }
    }
}
