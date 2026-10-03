using ClosedXML.Excel;
using MIS.Domain.Entities;
using MIS.Infrastructure.Services;
using Xunit;

namespace MIS.Domain.Tests;

public sealed class ExcuseImportMapperTests
{
    private static readonly Guid EmployeeId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static ExcuseImportMapper.EmployeeMatch Employee(
        string number = "E-001",
        bool active = true,
        DateOnly? hired = null,
        DateOnly? ended = null) =>
        new(EmployeeId, number, "Ahmed Hassan", active, false, hired ?? new DateOnly(2020, 1, 1), ended);

    private static Dictionary<string, ExcuseImportMapper.EmployeeMatch[]> Index(params ExcuseImportMapper.EmployeeMatch[] employees) =>
        ExcuseImportMapper.Index(employees);

    private static Dictionary<string, string> ValidRow(string? type = "إذن شخصي", string? from = "09:00", string? to = "11:00", string? date = "2026-10-03", string? number = "E-001", string? notes = "مراجعة جهة حكومية") =>
        new()
        {
            ["EmployeeNumber"] = number ?? "",
            ["ExcuseType"] = type ?? "",
            ["Date"] = date ?? "",
            ["FromTime"] = from ?? "",
            ["ToTime"] = to ?? "",
            ["Notes"] = notes ?? ""
        };

    [Fact]
    public void Valid_row_stays_pending_and_matches_arabic_type_with_extra_spaces()
    {
        var batch = new HashSet<(Guid, DateOnly, TimeOnly?, TimeOnly?)>();
        var row = ExcuseImportMapper.Map(2, ValidRow(type: "  إذن   شخصي  "), null, Index(Employee()), [], batch);

        Assert.Equal("Ready", row.Status);
        Assert.Empty(row.Errors);
        Assert.Equal("Ahmed Hassan", row.EmployeeName);
        Assert.Equal("E-001", row.EmployeeNumber);
        Assert.NotNull(row.Record);
        Assert.Equal("PersonalExcuse", row.Record.Type);
        Assert.Equal(new DateOnly(2026, 10, 3), row.Record.Date);
        Assert.Equal(new TimeOnly(9, 0), row.Record.FromTime);
        Assert.Equal(new TimeOnly(11, 0), row.Record.ToTime);
        Assert.False(row.Record.FullDay);
        Assert.Equal("مراجعة جهة حكومية", row.Record.Reason);
    }

    [Fact]
    public void Unknown_employee_number_is_invalid_and_does_not_invent_an_employee()
    {
        var row = ExcuseImportMapper.Map(2, ValidRow(number: "MISSING"), null, Index(Employee()), [], []);

        Assert.Equal("Error", row.Status);
        Assert.Null(row.Record);
        Assert.Contains("Employee number was not found.", row.Errors);
    }

    [Fact]
    public void Unknown_excuse_type_is_invalid()
    {
        var row = ExcuseImportMapper.Map(2, ValidRow(type: "FieldVisitMission"), null, Index(Employee()), [], []);

        Assert.Equal("Error", row.Status);
        Assert.Contains("Excuse type is invalid.", row.Errors);
    }

    [Fact]
    public void Duplicate_in_the_system_and_inside_the_file_are_rejected()
    {
        var employees = Index(Employee());
        var date = new DateOnly(2026, 10, 3);
        var period = (EmployeeId, date, (TimeOnly?)new TimeOnly(9, 0), (TimeOnly?)new TimeOnly(11, 0));
        var existing = ExcuseImportMapper.Map(2, ValidRow(), null, employees, [period], []);
        Assert.Equal("Error", existing.Status);
        Assert.Contains("An active manual excuse already exists for this employee, date and period.", existing.Errors);

        var batch = new HashSet<(Guid, DateOnly, TimeOnly?, TimeOnly?)>();
        var first = ExcuseImportMapper.Map(3, ValidRow(), null, employees, [], batch);
        var second = ExcuseImportMapper.Map(4, ValidRow(), null, employees, [], batch);
        Assert.Equal("Ready", first.Status);
        Assert.Equal("Error", second.Status);
        Assert.Contains("This excuse is duplicated in the file.", second.Errors);
    }

    [Fact]
    public void Invalid_date_reversed_times_and_a_single_time_are_rejected()
    {
        var employees = Index(Employee());
        var date = ExcuseImportMapper.Map(2, ValidRow(date: "not-a-date"), null, employees, [], []);
        Assert.Contains("Excuse date is invalid.", date.Errors);

        var reversed = ExcuseImportMapper.Map(3, ValidRow(from: "11:00", to: "09:00"), null, employees, [], []);
        Assert.Contains("End time must be after start time.", reversed.Errors);

        var oneSide = ExcuseImportMapper.Map(4, ValidRow(from: "09:00", to: ""), null, employees, [], []);
        Assert.Contains("From Time and To Time must both be entered, or both left empty for a full day.", oneSide.Errors);
        Assert.All(new[] { date, reversed, oneSide }, row => Assert.Equal("Error", row.Status));
    }

    [Fact]
    public void Empty_times_mean_a_full_day_and_empty_notes_are_rejected()
    {
        var fullDay = ExcuseImportMapper.Map(2, ValidRow(from: "", to: ""), null, Index(Employee()), [], []);
        Assert.Equal("Ready", fullDay.Status);
        Assert.True(fullDay.Record!.FullDay);
        Assert.Null(fullDay.Record.FromTime);
        Assert.Null(fullDay.Record.ToTime);

        var notes = ExcuseImportMapper.Map(3, ValidRow(notes: "  "), null, Index(Employee()), [], []);
        Assert.Contains("Notes are required because they are saved as the excuse reason.", notes.Errors);
    }

    [Fact]
    public void Inactive_employee_and_dates_outside_service_are_rejected()
    {
        var inactive = ExcuseImportMapper.Map(2, ValidRow(), null, Index(Employee(active: false)), [], []);
        Assert.Contains("Select an active employee for a manual excuse.", inactive.Errors);

        var beforeHire = ExcuseImportMapper.Map(3, ValidRow(date: "2019-12-31"), null, Index(Employee()), [], []);
        Assert.Contains("Excuse date cannot precede employment.", beforeHire.Errors);

        var afterExit = ExcuseImportMapper.Map(4, ValidRow(date: "2026-11-01"), null, Index(Employee(ended: new DateOnly(2026, 10, 1))), [], []);
        Assert.Contains("Excuse date is after the employee left service.", afterExit.Errors);
    }

    [Fact]
    public void Arabic_digits_and_excel_time_fractions_match()
    {
        var batch = new HashSet<(Guid, DateOnly, TimeOnly?, TimeOnly?)>();
        var row = ExcuseImportMapper.Map(2, ValidRow(number: "  ١٠١.0 ", from: "0.375", to: "0.4583333333"), null, Index(Employee(number: "101")), [], batch);

        Assert.Equal("Ready", row.Status);
        Assert.Equal(new TimeOnly(9, 0), row.Record!.FromTime);
        Assert.Equal(new TimeOnly(11, 0), row.Record.ToTime);
    }

    [Fact]
    public async Task Excel_date_cells_and_day_first_text_keep_the_calendar_date()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Excuses");
        foreach (var (column, header) in new[] { "Employee Number", "Excuse Type", "Date", "From Time", "To Time", "Notes" }.Select((header, index) => (index + 1, header)))
            sheet.Cell(1, column).Value = header;
        sheet.Range("C2:C20").Style.NumberFormat.Format = "@";
        void TextRow(int row, string dateText)
        {
            sheet.Cell(row, 1).Value = "1001";
            sheet.Cell(row, 2).Value = "إذن شخصي";
            sheet.Cell(row, 3).Value = dateText;
            sheet.Cell(row, 6).Value = "ملاحظة";
        }
        void DateRow(int row, DateTime date)
        {
            sheet.Cell(row, 1).Value = "1001";
            sheet.Cell(row, 2).Value = "إذن شخصي";
            sheet.Cell(row, 3).Value = date;
            sheet.Cell(row, 6).Value = "ملاحظة";
        }
        DateRow(2, new DateTime(2026, 8, 12));
        TextRow(3, "12/08/2026");
        TextRow(4, "01/02/2026");
        TextRow(5, "02/01/2026");
        TextRow(6, "11/08/2026");
        TextRow(7, "15/08/2026");
        TextRow(8, "10/08/2026");
        DateRow(9, new DateTime(2026, 8, 10));
        DateRow(10, new DateTime(2026, 11, 9));
        TextRow(11, "2026-08-12 00:00:00.0000000");
        TextRow(12, "2026-08-12T00:00:00.000Z");

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        var table = await AttendanceImportParser.ReadTableAsync(stream, ".xlsx", "Excuses", 1, 2, CancellationToken.None);
        var employees = Index(Employee(number: "1001", hired: new DateOnly(2026, 8, 11)));
        var mapped = table.Rows.Select(row => ExcuseImportMapper.Map(2, new Dictionary<string, string>
        {
            ["EmployeeNumber"] = row.ElementAtOrDefault(0) ?? "",
            ["ExcuseType"] = row.ElementAtOrDefault(1) ?? "",
            ["Date"] = row.ElementAtOrDefault(2) ?? "",
            ["Notes"] = row.ElementAtOrDefault(5) ?? ""
        }, null, employees, [], [])).ToArray();

        Assert.Equal(
            ["2026-08-12", "2026-08-12", "2026-02-01", "2026-01-02", "2026-08-11", "2026-08-15", "2026-08-10", "2026-08-10", "2026-11-09", "2026-08-12", "2026-08-12"],
            mapped.Select(row => row.Date ?? "").ToArray());
        Assert.Equal("Ready", mapped[0].Status);
        var afterHire = mapped[0].Record;
        Assert.NotNull(afterHire);
        Assert.Equal(new DateOnly(2026, 8, 12), afterHire.Date);
        Assert.Equal("Ready", mapped[1].Status);
        Assert.Equal("Error", mapped[2].Status);
        Assert.Contains("Excuse date cannot precede employment.", mapped[2].Errors);
        Assert.NotEqual("2026-01-02", mapped[2].Date);
        Assert.Equal("Error", mapped[3].Status);
        Assert.Contains("Excuse date cannot precede employment.", mapped[3].Errors);
        Assert.Equal("Ready", mapped[4].Status);
        Assert.Equal("Ready", mapped[5].Status);
        Assert.Equal(new DateOnly(2026, 8, 15), mapped[5].Record!.Date);
        Assert.Equal("Error", mapped[6].Status);
        Assert.Contains("Excuse date cannot precede employment.", mapped[6].Errors);
        Assert.Equal("Error", mapped[7].Status);
        Assert.Equal("Ready", mapped[8].Status);
        Assert.Equal(new DateOnly(2026, 11, 9), mapped[8].Record!.Date);

        var stored = new HrExcuseMission(afterHire.EmployeeId, afterHire.Type, afterHire.Date, null, null, afterHire.Reason, afterHire.Reason, Guid.NewGuid(), DateTimeOffset.UtcNow);
        Assert.Equal(new DateOnly(2026, 8, 12), stored.Date);
        Assert.Equal("PendingApproval", stored.Status);
    }

    [Fact]
    public void Ambiguous_employee_number_is_rejected()
    {
        var employees = Index(Employee(number: "E-001"), new ExcuseImportMapper.EmployeeMatch(Guid.NewGuid(), "e-001", "Other Person", true, false, new DateOnly(2020, 1, 1), null));
        var row = ExcuseImportMapper.Map(2, ValidRow(), null, employees, [], []);

        Assert.Equal("Error", row.Status);
        Assert.Contains("Employee number matches more than one employee.", row.Errors);
        Assert.Equal("", row.EmployeeName);
    }
}
