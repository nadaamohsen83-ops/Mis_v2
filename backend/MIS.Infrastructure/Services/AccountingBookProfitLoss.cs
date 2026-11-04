using Microsoft.EntityFrameworkCore;
using MIS.Application.DTOs.Accounting;
using MIS.Domain.Constants;

namespace MIS.Infrastructure.Services;

public sealed partial class AccountingBookService
{
    public async Task<AccountingProfitLossDto> ProfitLossAsync(DateOnly from, DateOnly to, CancellationToken token)
    {
        EnsureAccess();
        CheckDates(from, to);

        var lines = await db.JournalEntryLines.AsNoTracking()
            .Where(x => (x.JournalEntry.Status == "POSTED" || x.JournalEntry.Status == "REVERSED")
                && x.JournalEntry.PostingDate >= from && x.JournalEntry.PostingDate <= to
                && (x.Account.AccountType == "REVENUE" || x.Account.AccountType == "EXPENSE")
                && x.JournalEntry.EntryType != "ACCOUNTING_DELETE"
                && !db.JournalEntries.Any(reversal => reversal.ReversalOfJournalId == x.JournalEntryId && reversal.EntryType == "ACCOUNTING_DELETE"))
            .Select(x => new
            {
                x.JournalEntryId,
                x.JournalEntry.PostingDate,
                x.JournalEntry.JournalNumber,
                x.JournalEntry.Description,
                x.Account.Code,
                x.Account.AccountType,
                x.BaseDebit,
                x.BaseCredit
            })
            .ToListAsync(token);

        var transactions = lines
            .GroupBy(x => new { x.JournalEntryId, x.JournalNumber, x.PostingDate, x.Description, x.Code, x.AccountType })
            .Select(group => new ProfitLossMovement
            {
                Category = group.Key.AccountType == "REVENUE"
                    ? "REVENUE"
                    : AccountingBookValues.Categories.FirstOrDefault(category =>
                        AccountingBookValues.AccountType(category) == "EXPENSE" &&
                        AccountingBookValues.AccountCode(category) == group.Key.Code) ?? "OTHER",
                Type = group.Key.AccountType,
                Transaction = new(
                    group.Key.PostingDate,
                    group.Key.JournalNumber,
                    group.Key.Description,
                    group.Key.AccountType == "REVENUE"
                        ? group.Sum(x => x.BaseCredit - x.BaseDebit)
                        : group.Sum(x => x.BaseDebit - x.BaseCredit))
            })
            .Where(x => x.Transaction.Amount != 0)
            .ToList();

        var legacyExpenses = await LegacyExpenses(from, to, token);
        foreach (var legacy in legacyExpenses)
        {
            var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(
                legacy.PaidAt, OperatingSystem.IsWindows() ? "Egypt Standard Time" : "Africa/Cairo").DateTime);
            var description = legacy.Description;
            if (legacy.Kind == "PAYROLL")
            {
                transactions.Add(new ProfitLossMovement
                {
                    Category = "SALARY",
                    Type = "EXPENSE",
                    Transaction = new(date, null, description,
                        legacy.Amount - legacy.Transportation - legacy.Commissions - legacy.Bonuses)
                });
                if (legacy.Transportation != 0)
                    transactions.Add(new ProfitLossMovement { Category = "TRANSPORTATION", Type = "EXPENSE", Transaction = new(date, null, description, legacy.Transportation) });
                if (legacy.Commissions != 0)
                    transactions.Add(new ProfitLossMovement { Category = "PAYROLL_COMMISSION", Type = "EXPENSE", Transaction = new(date, null, description, legacy.Commissions) });
                if (legacy.Bonuses != 0)
                    transactions.Add(new ProfitLossMovement { Category = "BONUS", Type = "EXPENSE", Transaction = new(date, null, description, legacy.Bonuses) });
            }
            else
            {
                transactions.Add(new ProfitLossMovement
                {
                    Category = legacy.Category,
                    Type = "EXPENSE",
                    Transaction = new(date, null, description, legacy.Amount)
                });
            }
        }

        IReadOnlyList<AccountingProfitLossCategoryDto> Categories(string type) =>
            transactions.Where(x => x.Type == type)
                .GroupBy(x => x.Category)
                .Select(group => new AccountingProfitLossCategoryDto(
                    group.Key,
                    group.Sum(x => x.Transaction.Amount),
                    group.Select(x => x.Transaction).OrderBy(x => x.Date).ThenBy(x => x.JournalNumber).ToArray()))
                .Where(x => x.Amount != 0)
                .OrderBy(x => x.Category)
                .ToArray();

        var revenueCategories = Categories("REVENUE");
        var expenseCategories = Categories("EXPENSE");
        var revenue = revenueCategories.Sum(x => x.Amount);
        var expenses = expenseCategories.Sum(x => x.Amount);
        return new(from, to, revenue, expenses, revenue - expenses, revenueCategories, expenseCategories);
    }

    private sealed class ProfitLossMovement
    {
        public required string Category { get; init; }
        public required string Type { get; init; }
        public required AccountingProfitLossTransactionDto Transaction { get; init; }
    }
}
