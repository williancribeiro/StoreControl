using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StoreControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SalesAndCash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "cash");

            migrationBuilder.EnsureSchema(
                name: "sales");

            migrationBuilder.CreateTable(
                name: "CashRegisters",
                schema: "cash",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashRegisters", x => x.Id);
                    table.CheckConstraint("CK_CashRegisters_Code_NotBlank", "LEN(LTRIM(RTRIM([Code]))) > 0");
                    table.CheckConstraint("CK_CashRegisters_Name_NotBlank", "LEN(LTRIM(RTRIM([Name]))) > 0");
                    table.ForeignKey(
                        name: "FK_CashRegisters_Stores_StoreId",
                        column: x => x.StoreId,
                        principalSchema: "organization",
                        principalTable: "Stores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CashSessions",
                schema: "cash",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CashRegisterId = table.Column<int>(type: "int", nullable: false),
                    OpenedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OpenedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    ClosedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClosedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    OpeningAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ExpectedCashAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DeclaredClosingAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    DifferenceAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashSessions", x => x.Id);
                    table.CheckConstraint("CK_CashSessions_Amounts_NonNegative", "[OpeningAmount] >= 0 AND [ExpectedCashAmount] >= 0 AND ([DeclaredClosingAmount] IS NULL OR [DeclaredClosingAmount] >= 0)");
                    table.CheckConstraint("CK_CashSessions_ClosingState", "([Status] = 1 AND [ClosedByUserId] IS NULL AND [ClosedAtUtc] IS NULL AND [DeclaredClosingAmount] IS NULL AND [DifferenceAmount] IS NULL) OR ([Status] = 2 AND [ClosedByUserId] IS NOT NULL AND [ClosedAtUtc] IS NOT NULL AND [DeclaredClosingAmount] IS NOT NULL AND [DifferenceAmount] IS NOT NULL)");
                    table.CheckConstraint("CK_CashSessions_Status", "[Status] BETWEEN 1 AND 2");
                    table.ForeignKey(
                        name: "FK_CashSessions_CashRegisters_CashRegisterId",
                        column: x => x.CashRegisterId,
                        principalSchema: "cash",
                        principalTable: "CashRegisters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashSessions_Users_ClosedByUserId",
                        column: x => x.ClosedByUserId,
                        principalSchema: "security",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashSessions_Users_OpenedByUserId",
                        column: x => x.OpenedByUserId,
                        principalSchema: "security",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Sales",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    CashSessionId = table.Column<long>(type: "bigint", nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    FinalizedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    Subtotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sales", x => x.Id);
                    table.CheckConstraint("CK_Sales_Discount_Range", "[DiscountAmount] >= 0 AND [DiscountAmount] < [Subtotal]");
                    table.CheckConstraint("CK_Sales_Status_Finalized", "[Status] = 2");
                    table.CheckConstraint("CK_Sales_Subtotal_Positive", "[Subtotal] > 0");
                    table.CheckConstraint("CK_Sales_Total_Equation", "[TotalAmount] = [Subtotal] - [DiscountAmount] AND [TotalAmount] > 0");
                    table.ForeignKey(
                        name: "FK_Sales_CashSessions_CashSessionId",
                        column: x => x.CashSessionId,
                        principalSchema: "cash",
                        principalTable: "CashSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Sales_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "parties",
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Sales_Stores_StoreId",
                        column: x => x.StoreId,
                        principalSchema: "organization",
                        principalTable: "Stores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Sales_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "security",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SaleItems",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SaleId = table.Column<long>(type: "bigint", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    ProductCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ProductName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    LineSubtotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaleItems", x => x.Id);
                    table.CheckConstraint("CK_SaleItems_LineSubtotal_NonNegative", "[LineSubtotal] >= 0");
                    table.CheckConstraint("CK_SaleItems_ProductCode_NotBlank", "LEN(LTRIM(RTRIM([ProductCode]))) > 0");
                    table.CheckConstraint("CK_SaleItems_ProductName_NotBlank", "LEN(LTRIM(RTRIM([ProductName]))) > 0");
                    table.CheckConstraint("CK_SaleItems_Quantity_Positive", "[Quantity] > 0");
                    table.CheckConstraint("CK_SaleItems_UnitPrice_NonNegative", "[UnitPrice] >= 0");
                    table.ForeignKey(
                        name: "FK_SaleItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "catalog",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SaleItems_Sales_SaleId",
                        column: x => x.SaleId,
                        principalSchema: "sales",
                        principalTable: "Sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SalePayments",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SaleId = table.Column<long>(type: "bigint", nullable: false),
                    PaymentMethod = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalePayments", x => x.Id);
                    table.CheckConstraint("CK_SalePayments_Amount_Positive", "[Amount] > 0");
                    table.CheckConstraint("CK_SalePayments_Method", "[PaymentMethod] BETWEEN 1 AND 6");
                    table.ForeignKey(
                        name: "FK_SalePayments_Sales_SaleId",
                        column: x => x.SaleId,
                        principalSchema: "sales",
                        principalTable: "Sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CashMovements",
                schema: "cash",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CashSessionId = table.Column<long>(type: "bigint", nullable: false),
                    MovementType = table.Column<int>(type: "int", nullable: false),
                    PaymentMethod = table.Column<int>(type: "int", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ReferenceType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ReferenceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SalePaymentId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashMovements", x => x.Id);
                    table.CheckConstraint("CK_CashMovements_Reason_NotBlank", "LEN(LTRIM(RTRIM([Reason]))) > 0");
                    table.CheckConstraint("CK_CashMovements_Reference", "([ReferenceType] IS NULL AND [ReferenceId] IS NULL) OR ([ReferenceType] IS NOT NULL AND [ReferenceId] IS NOT NULL)");
                    table.CheckConstraint("CK_CashMovements_SalePayment", "([MovementType] = 2 AND [PaymentMethod] BETWEEN 1 AND 6 AND [SalePaymentId] IS NOT NULL) OR ([MovementType] <> 2 AND [PaymentMethod] IS NULL AND [SalePaymentId] IS NULL)");
                    table.CheckConstraint("CK_CashMovements_TypeDirection", "([MovementType] IN (1, 2, 3) AND [Amount] > 0) OR ([MovementType] = 4 AND [Amount] < 0)");
                    table.ForeignKey(
                        name: "FK_CashMovements_CashSessions_CashSessionId",
                        column: x => x.CashSessionId,
                        principalSchema: "cash",
                        principalTable: "CashSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashMovements_SalePayments_SalePaymentId",
                        column: x => x.SalePaymentId,
                        principalSchema: "sales",
                        principalTable: "SalePayments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashMovements_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "security",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CashMovements_CashSessionId_OccurredAtUtc",
                schema: "cash",
                table: "CashMovements",
                columns: new[] { "CashSessionId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CashMovements_CreatedByUserId",
                schema: "cash",
                table: "CashMovements",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CashMovements_OperationId",
                schema: "cash",
                table: "CashMovements",
                column: "OperationId",
                unique: true,
                filter: "[OperationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CashMovements_ReferenceType_ReferenceId",
                schema: "cash",
                table: "CashMovements",
                columns: new[] { "ReferenceType", "ReferenceId" },
                filter: "[ReferenceType] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CashMovements_SalePaymentId",
                schema: "cash",
                table: "CashMovements",
                column: "SalePaymentId",
                unique: true,
                filter: "[SalePaymentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CashRegisters_StoreId_Code",
                schema: "cash",
                table: "CashRegisters",
                columns: new[] { "StoreId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashSessions_CashRegisterId_Status",
                schema: "cash",
                table: "CashSessions",
                columns: new[] { "CashRegisterId", "Status" },
                unique: true,
                filter: "[Status] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_CashSessions_ClosedByUserId",
                schema: "cash",
                table: "CashSessions",
                column: "ClosedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CashSessions_OpenedAtUtc",
                schema: "cash",
                table: "CashSessions",
                column: "OpenedAtUtc",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_CashSessions_OpenedByUserId",
                schema: "cash",
                table: "CashSessions",
                column: "OpenedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CashSessions_OperationId",
                schema: "cash",
                table: "CashSessions",
                column: "OperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SaleItems_ProductId",
                schema: "sales",
                table: "SaleItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleItems_SaleId_ProductId",
                schema: "sales",
                table: "SaleItems",
                columns: new[] { "SaleId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalePayments_SaleId_PaymentMethod",
                schema: "sales",
                table: "SalePayments",
                columns: new[] { "SaleId", "PaymentMethod" });

            migrationBuilder.CreateIndex(
                name: "IX_Sales_CashSessionId",
                schema: "sales",
                table: "Sales",
                column: "CashSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_CreatedByUserId",
                schema: "sales",
                table: "Sales",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_CustomerId_FinalizedAtUtc",
                schema: "sales",
                table: "Sales",
                columns: new[] { "CustomerId", "FinalizedAtUtc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Sales_OperationId",
                schema: "sales",
                table: "Sales",
                column: "OperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sales_StoreId_FinalizedAtUtc",
                schema: "sales",
                table: "Sales",
                columns: new[] { "StoreId", "FinalizedAtUtc" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CashMovements",
                schema: "cash");

            migrationBuilder.DropTable(
                name: "SaleItems",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "SalePayments",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "Sales",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "CashSessions",
                schema: "cash");

            migrationBuilder.DropTable(
                name: "CashRegisters",
                schema: "cash");
        }
    }
}
