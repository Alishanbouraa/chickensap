using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace PoultrySlaughterPOS.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AUDIT_LOGS",
                columns: table => new
                {
                    AuditId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TableName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Operation = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    OldValues = table.Column<string>(type: "ntext", nullable: true),
                    NewValues = table.Column<string>(type: "ntext", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AUDIT_LOGS", x => x.AuditId);
                });

            migrationBuilder.CreateTable(
                name: "CUSTOMERS",
                columns: table => new
                {
                    CustomerId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TotalDebt = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 0m),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CUSTOMERS", x => x.CustomerId);
                });

            migrationBuilder.CreateTable(
                name: "TRUCKS",
                columns: table => new
                {
                    TruckId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TruckNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DriverName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TRUCKS", x => x.TruckId);
                });

            migrationBuilder.CreateTable(
                name: "DAILY_RECONCILIATION",
                columns: table => new
                {
                    ReconciliationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TruckId = table.Column<int>(type: "int", nullable: false),
                    ReconciliationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LoadWeight = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    SoldWeight = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    WastageWeight = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    WastagePercentage = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "PENDING"),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DAILY_RECONCILIATION", x => x.ReconciliationId);
                    table.ForeignKey(
                        name: "FK_DAILY_RECONCILIATION_TRUCKS_TruckId",
                        column: x => x.TruckId,
                        principalTable: "TRUCKS",
                        principalColumn: "TruckId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INVOICES",
                columns: table => new
                {
                    InvoiceId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    TruckId = table.Column<int>(type: "int", nullable: false),
                    InvoiceDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GrossWeight = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    CagesWeight = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    CagesCount = table.Column<int>(type: "int", nullable: false),
                    NetWeight = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    DiscountPercentage = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    FinalAmount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    PreviousBalance = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    CurrentBalance = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INVOICES", x => x.InvoiceId);
                    table.ForeignKey(
                        name: "FK_INVOICES_CUSTOMERS_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "CUSTOMERS",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INVOICES_TRUCKS_TruckId",
                        column: x => x.TruckId,
                        principalTable: "TRUCKS",
                        principalColumn: "TruckId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TRUCK_LOADS",
                columns: table => new
                {
                    LoadId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TruckId = table.Column<int>(type: "int", nullable: false),
                    LoadDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TotalWeight = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    CagesCount = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "LOADED"),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TRUCK_LOADS", x => x.LoadId);
                    table.ForeignKey(
                        name: "FK_TRUCK_LOADS_TRUCKS_TruckId",
                        column: x => x.TruckId,
                        principalTable: "TRUCKS",
                        principalColumn: "TruckId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PAYMENTS",
                columns: table => new
                {
                    PaymentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    InvoiceId = table.Column<int>(type: "int", nullable: true),
                    PaymentDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    PaymentMethod = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "CASH"),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PAYMENTS", x => x.PaymentId);
                    table.ForeignKey(
                        name: "FK_PAYMENTS_CUSTOMERS_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "CUSTOMERS",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PAYMENTS_INVOICES_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "INVOICES",
                        principalColumn: "InvoiceId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.InsertData(
                table: "CUSTOMERS",
                columns: new[] { "CustomerId", "Address", "CreatedDate", "CustomerName", "IsActive", "PhoneNumber", "UpdatedDate" },
                values: new object[,]
                {
                    { 1, "بغداد - الكرادة", new DateTime(2025, 5, 27, 13, 4, 57, 615, DateTimeKind.Local).AddTicks(2379), "سوق الجملة المركزي", true, "07901234567", new DateTime(2025, 5, 27, 13, 4, 57, 615, DateTimeKind.Local).AddTicks(2381) },
                    { 2, "بغداد - الجادرية", new DateTime(2025, 5, 27, 13, 4, 57, 615, DateTimeKind.Local).AddTicks(2389), "مطعم الأصالة", true, "07801234567", new DateTime(2025, 5, 27, 13, 4, 57, 615, DateTimeKind.Local).AddTicks(2390) },
                    { 3, "بغداد - الأعظمية", new DateTime(2025, 5, 27, 13, 4, 57, 615, DateTimeKind.Local).AddTicks(2397), "متجر الطازج للدواجن", true, "07901234568", new DateTime(2025, 5, 27, 13, 4, 57, 615, DateTimeKind.Local).AddTicks(2399) }
                });

            migrationBuilder.InsertData(
                table: "TRUCKS",
                columns: new[] { "TruckId", "CreatedDate", "DriverName", "IsActive", "TruckNumber", "UpdatedDate" },
                values: new object[,]
                {
                    { 1, new DateTime(2025, 5, 27, 13, 4, 57, 615, DateTimeKind.Local).AddTicks(2192), "أحمد محمد", true, "TR-001", new DateTime(2025, 5, 27, 13, 4, 57, 615, DateTimeKind.Local).AddTicks(2194) },
                    { 2, new DateTime(2025, 5, 27, 13, 4, 57, 615, DateTimeKind.Local).AddTicks(2202), "محمد علي", true, "TR-002", new DateTime(2025, 5, 27, 13, 4, 57, 615, DateTimeKind.Local).AddTicks(2204) },
                    { 3, new DateTime(2025, 5, 27, 13, 4, 57, 615, DateTimeKind.Local).AddTicks(2211), "علي حسن", true, "TR-003", new DateTime(2025, 5, 27, 13, 4, 57, 615, DateTimeKind.Local).AddTicks(2213) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AUDIT_LOGS_TableName_CreatedDate",
                table: "AUDIT_LOGS",
                columns: new[] { "TableName", "CreatedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_CUSTOMERS_CustomerName",
                table: "CUSTOMERS",
                column: "CustomerName");

            migrationBuilder.CreateIndex(
                name: "IX_DAILY_RECONCILIATION_TruckId_ReconciliationDate",
                table: "DAILY_RECONCILIATION",
                columns: new[] { "TruckId", "ReconciliationDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INVOICES_CustomerId",
                table: "INVOICES",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_INVOICES_InvoiceNumber",
                table: "INVOICES",
                column: "InvoiceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INVOICES_TruckId",
                table: "INVOICES",
                column: "TruckId");

            migrationBuilder.CreateIndex(
                name: "IX_PAYMENTS_CustomerId",
                table: "PAYMENTS",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_PAYMENTS_InvoiceId",
                table: "PAYMENTS",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_TRUCK_LOADS_TruckId",
                table: "TRUCK_LOADS",
                column: "TruckId");

            migrationBuilder.CreateIndex(
                name: "IX_TRUCKS_TruckNumber",
                table: "TRUCKS",
                column: "TruckNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AUDIT_LOGS");

            migrationBuilder.DropTable(
                name: "DAILY_RECONCILIATION");

            migrationBuilder.DropTable(
                name: "PAYMENTS");

            migrationBuilder.DropTable(
                name: "TRUCK_LOADS");

            migrationBuilder.DropTable(
                name: "INVOICES");

            migrationBuilder.DropTable(
                name: "CUSTOMERS");

            migrationBuilder.DropTable(
                name: "TRUCKS");
        }
    }
}
