using MIS.Domain.Constants;
using MIS.Domain.Entities;
using Xunit;

namespace MIS.Domain.Tests;

public sealed class AccountingDomainTests
{
    [Fact]
    public void EmployeePayroll_NetSalary_UsesComponentFormula()
    {
        var payroll = new AccountingEmployeePayroll(
            Guid.NewGuid(), Guid.NewGuid(), "E-100", "Test Employee", "HR", "Officer",
            basicSalary: 5000m, allowances: 500m, compensationSnapshotId: null, createdAt: DateTimeOffset.UtcNow);

        payroll.ApplyComponents(
            transportation: 200m,
            commissions: 150m,
            bonuses: 100m,
            deductions: 50m,
            otherAdjustments: 25m,
            notes: null,
            now: DateTimeOffset.UtcNow);

        Assert.Equal(5925m, payroll.NetSalary);
    }

    [Fact]
    public void EmployeePayroll_Approved_CannotBeSilentlyEdited()
    {
        var payroll = new AccountingEmployeePayroll(
            Guid.NewGuid(), Guid.NewGuid(), "E-101", "Locked Employee", null, null,
            3000m, 0m, null, DateTimeOffset.UtcNow);
        payroll.Approve(DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() =>
            payroll.ApplyComponents(10m, 0m, 0m, 0m, 0m, null, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void EmployeePayroll_StatusControlsPaymentSeparatelyFromPayrollWorkflow()
    {
        var start = new DateOnly(2026, 10, 1);
        var suspended = NewPayroll(AccountingValues.EmployeePayrollStatuses.Suspended);
        var unpaidLeave = NewPayroll(AccountingValues.EmployeePayrollStatuses.UnpaidLeave);
        var terminatedWithoutDate = NewPayroll(AccountingValues.EmployeePayrollStatuses.Terminated);
        var terminatedDuringMonth = NewPayroll(AccountingValues.EmployeePayrollStatuses.Terminated);
        var activeAfterTermination = NewPayroll(AccountingValues.EmployeePayrollStatuses.Active);

        Assert.False(suspended.IsPaymentEligible(start, null));
        Assert.True(unpaidLeave.IsPaymentEligible(start, null));
        Assert.False(terminatedWithoutDate.IsPaymentEligible(start, null));
        Assert.True(terminatedDuringMonth.IsPaymentEligible(start, new DateOnly(2026, 10, 15)));
        Assert.False(terminatedDuringMonth.IsPaymentEligible(start, new DateOnly(2026, 9, 30)));
        Assert.False(activeAfterTermination.IsPaymentEligible(start, new DateOnly(2026, 9, 30)));
        Assert.Equal(AccountingValues.PayrollStatuses.Draft, suspended.Status);
    }

    [Fact]
    public void EmployeePayroll_StatusChangeRequiresActorAndCannotChangePaidRow()
    {
        var payroll = NewPayroll(AccountingValues.EmployeePayrollStatuses.Active);
        var actor = Guid.NewGuid();
        payroll.ChangeEmployeePayrollStatus(AccountingValues.EmployeePayrollStatuses.Suspended, "Investigation", actor, DateTimeOffset.UtcNow);
        Assert.Equal(AccountingValues.EmployeePayrollStatuses.Suspended, payroll.EmployeePayrollStatus);
        Assert.Equal("Investigation", payroll.EmployeePayrollStatusReason);
        Assert.Equal(actor, payroll.EmployeePayrollStatusChangedByUserId);

        payroll.Approve(DateTimeOffset.UtcNow);
        payroll.MarkPaid(DateTimeOffset.UtcNow);
        Assert.Throws<InvalidOperationException>(() =>
            payroll.ChangeEmployeePayrollStatus(AccountingValues.EmployeePayrollStatuses.Active, null, actor, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void CommissionRule_PercentOfCollected_RoundsAwayFromZero()
    {
        var rule = new AccountingCommissionRule(
            "COL-5", "عمولة 5%", "Collector 5%", AccountingValues.CommissionScopes.Collector,
            AccountingValues.CommissionBases.PercentOfCollected, 5m, null, new DateOnly(2026, 1, 1),
            Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.Equal(12.35m, rule.Calculate(247m));
    }

    private static AccountingEmployeePayroll NewPayroll(string employeePayrollStatus) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "E-200", "Test Employee", null, null, 3000m, 0m, null, DateTimeOffset.UtcNow, employeePayrollStatus);
}
