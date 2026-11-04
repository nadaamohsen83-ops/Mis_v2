using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MIS.Infrastructure.Persistence;

#nullable disable

namespace MIS.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261101221800_SeparateCompanyIncomeFromRevenue")]
public partial class SeparateCompanyIncomeFromRevenue : Migration
{
    private const string CompanyIncomeAccountId = "10000000-0000-0000-0002-000000210300";
    private const string RevenueAccountId = "10000000-0000-0000-0004-000000410100";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql($"""
            INSERT INTO finance.accounts
                ("Id", "AccountType", "Code", "ControlAccountType", "IsActive", "IsCashAccount", "LegalEntityId", "NameArabic", "NameEnglish", "NormalBalance", "ParentId", "PostingAllowed", "RequiresBranch", "RequiresClient", "RequiresCollector")
            VALUES
                ('{CompanyIncomeAccountId}', 'LIABILITY', '210300', NULL, TRUE, FALSE, '10000000-0000-0000-0000-000000000001', 'وارد الشركة تحت التصنيف', 'Company receipts clearing', 'CREDIT', NULL, TRUE, FALSE, FALSE, FALSE);

            UPDATE finance.journal_entry_lines AS line
            SET "AccountId" = '{CompanyIncomeAccountId}'
            FROM public."AccountingBookEntries" AS entry
            WHERE entry."Category" = 'INCOME'
              AND entry."JournalEntryId" = line."JournalEntryId"
              AND line."AccountId" = entry."AccountId";

            UPDATE finance.journal_entry_lines AS line
            SET "AccountId" = '{CompanyIncomeAccountId}'
            FROM finance.journal_entries AS reversal
            JOIN public."AccountingBookEntries" AS entry ON reversal."ReversalOfJournalId" = entry."JournalEntryId"
            WHERE entry."Category" = 'INCOME'
              AND reversal."Id" = line."JournalEntryId"
              AND line."AccountId" = entry."AccountId";

            UPDATE public."AccountingBookEntries"
            SET "AccountId" = '{CompanyIncomeAccountId}'
            WHERE "Category" = 'INCOME' AND "AccountId" <> '{CompanyIncomeAccountId}';

            UPDATE finance.journal_entries AS journal
            SET "EntryType" = 'COMPANY_INCOME'
            FROM public."AccountingBookEntries" AS entry
            WHERE entry."Category" = 'INCOME'
              AND entry."JournalEntryId" = journal."Id";
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql($"""
            UPDATE finance.journal_entry_lines AS line
            SET "AccountId" = '{RevenueAccountId}'
            FROM public."AccountingBookEntries" AS entry
            WHERE entry."Category" = 'INCOME'
              AND entry."JournalEntryId" = line."JournalEntryId"
              AND line."AccountId" = '{CompanyIncomeAccountId}';

            UPDATE finance.journal_entry_lines AS line
            SET "AccountId" = '{RevenueAccountId}'
            FROM finance.journal_entries AS reversal
            JOIN public."AccountingBookEntries" AS entry ON reversal."ReversalOfJournalId" = entry."JournalEntryId"
            WHERE entry."Category" = 'INCOME'
              AND reversal."Id" = line."JournalEntryId"
              AND line."AccountId" = '{CompanyIncomeAccountId}';

            UPDATE public."AccountingBookEntries"
            SET "AccountId" = '{RevenueAccountId}'
            WHERE "Category" = 'INCOME' AND "AccountId" = '{CompanyIncomeAccountId}';

            UPDATE finance.journal_entries AS journal
            SET "EntryType" = 'ACCOUNTING_OPERATION'
            FROM public."AccountingBookEntries" AS entry
            WHERE entry."Category" = 'INCOME'
              AND entry."JournalEntryId" = journal."Id";
            """);

        migrationBuilder.Sql($"DELETE FROM finance.accounts WHERE \"Id\" = '{CompanyIncomeAccountId}';");
    }
}
