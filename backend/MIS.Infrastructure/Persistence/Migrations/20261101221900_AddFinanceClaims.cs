using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MIS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFinanceClaims : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "claim_import_batches",
                schema: "finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    FileHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PreviewJson = table.Column<string>(type: "jsonb", nullable: false),
                    TotalRows = table.Column<int>(type: "integer", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConfirmedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConfirmedById = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_claim_import_batches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_claim_import_batches_Users_ConfirmedById",
                        column: x => x.ConfirmedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_claim_import_batches_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "claims",
                schema: "finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Serial = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    BankOrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    BankName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ClaimType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Month = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ClaimDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AmountWithoutVat = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    VatPercent = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    Vat = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalWithVat = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxAtSourcePercent = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    TaxAtSource = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalInBank = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DateInBank = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsHistoricalImport = table.Column<bool>(type: "boolean", nullable: false),
                    ImportBatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReceiptConfirmedById = table.Column<Guid>(type: "uuid", nullable: true),
                    ReceiptConfirmedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelledById = table.Column<Guid>(type: "uuid", nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_claims", x => x.Id);
                    table.CheckConstraint("CK_FinanceClaims_Status", "\"Status\" IN ('PENDING','RECEIVED','CANCELLED')");
                    table.ForeignKey(
                        name: "FK_claims_CollectionClientOrganizations_BankOrganizationId",
                        column: x => x.BankOrganizationId,
                        principalTable: "CollectionClientOrganizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_claims_Users_CancelledById",
                        column: x => x.CancelledById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_claims_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_claims_Users_ReceiptConfirmedById",
                        column: x => x.ReceiptConfirmedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_claims_Users_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_claims_claim_import_batches_ImportBatchId",
                        column: x => x.ImportBatchId,
                        principalSchema: "finance",
                        principalTable: "claim_import_batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "claim_receipt_postings",
                schema: "finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountingBookEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ReceiptDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IsHistoricalPosting = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReversedById = table.Column<Guid>(type: "uuid", nullable: true),
                    ReversedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReversalJournalId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_claim_receipt_postings", x => x.Id);
                    table.CheckConstraint("CK_FinanceClaimReceiptPostings_Amount", "\"Amount\" > 0");
                    table.ForeignKey(
                        name: "FK_claim_receipt_postings_AccountingBookEntries_AccountingBook~",
                        column: x => x.AccountingBookEntryId,
                        principalTable: "AccountingBookEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_claim_receipt_postings_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_claim_receipt_postings_Users_ReversedById",
                        column: x => x.ReversedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_claim_receipt_postings_claims_ClaimId",
                        column: x => x.ClaimId,
                        principalSchema: "finance",
                        principalTable: "claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_claim_import_batches_ConfirmedById",
                schema: "finance",
                table: "claim_import_batches",
                column: "ConfirmedById");

            migrationBuilder.CreateIndex(
                name: "IX_claim_import_batches_CreatedById",
                schema: "finance",
                table: "claim_import_batches",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_claim_import_batches_FileHash",
                schema: "finance",
                table: "claim_import_batches",
                column: "FileHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_claim_receipt_postings_AccountingBookEntryId",
                schema: "finance",
                table: "claim_receipt_postings",
                column: "AccountingBookEntryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_claim_receipt_postings_ClaimId",
                schema: "finance",
                table: "claim_receipt_postings",
                column: "ClaimId",
                unique: true,
                filter: "\"ReversedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_claim_receipt_postings_CreatedById",
                schema: "finance",
                table: "claim_receipt_postings",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_claim_receipt_postings_ReversedById",
                schema: "finance",
                table: "claim_receipt_postings",
                column: "ReversedById");

            migrationBuilder.CreateIndex(
                name: "IX_claims_BankOrganizationId_ClaimType_Month",
                schema: "finance",
                table: "claims",
                columns: new[] { "BankOrganizationId", "ClaimType", "Month" });

            migrationBuilder.CreateIndex(
                name: "IX_claims_CancelledById",
                schema: "finance",
                table: "claims",
                column: "CancelledById");

            migrationBuilder.CreateIndex(
                name: "IX_claims_ClaimDate_Status",
                schema: "finance",
                table: "claims",
                columns: new[] { "ClaimDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_claims_CreatedById",
                schema: "finance",
                table: "claims",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_claims_ImportBatchId",
                schema: "finance",
                table: "claims",
                column: "ImportBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_claims_ReceiptConfirmedById",
                schema: "finance",
                table: "claims",
                column: "ReceiptConfirmedById");

            migrationBuilder.CreateIndex(
                name: "IX_claims_UpdatedById",
                schema: "finance",
                table: "claims",
                column: "UpdatedById");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "claim_receipt_postings",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "claims",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "claim_import_batches",
                schema: "finance");
        }
    }
}
