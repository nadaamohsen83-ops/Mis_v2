using ClosedXML.Excel;
using MIS.Application.DTOs.Collections;
using MIS.Domain.Hr;
using MIS.Infrastructure.Services;
using Xunit;

namespace MIS.Domain.Tests;

public sealed class VisitImportPlannerTests
{
    private static readonly Guid CaseId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid CollectorId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid OtherId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly VisitImportPlanner.Actor Manager = new(true, Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"), Person(Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"), "Manager", "manager", "EMP-9"));
    private static readonly VisitImportPlanner.Actor Collector = new(false, CollectorId, Person(CollectorId, "Nada", "nada", "EMP-1001"));

    [Fact]
    public void Day_first_dates_and_excel_serials_keep_the_calendar_day()
    {
        Assert.Equal(new DateOnly(2026, 8, 12), ParseDate("12/08/2026"));
        Assert.Equal(new DateOnly(2026, 2, 1), ParseDate("01/02/2026"));
        Assert.Equal(new DateOnly(2026, 1, 2), ParseDate("02/01/2026"));
        Assert.Equal(new DateOnly(2026, 8, 12), ParseDate("2026-08-12T00:00:00.000Z"));
        Assert.Equal(new DateOnly(2026, 8, 12), ParseDate("2026-08-12 00:00:00.0000000"));
        Assert.Equal(new DateOnly(2026, 11, 9), ParseDate("46335"));
        Assert.False(VisitImportPlanner.TryDate("32/08/2026", out _));
        Assert.True(VisitImportPlanner.TryTime("09:30", out var time));
        Assert.Equal(new TimeOnly(9, 30), new TimeOnly(time.Hour, time.Minute));
        Assert.True(VisitImportPlanner.TryTime("0.375", out var fraction));
        Assert.Equal(new TimeOnly(9, 0), new TimeOnly(fraction.Hour, fraction.Minute));
        var instant = VisitImportPlanner.ComposeCairo(new DateOnly(2026, 8, 12), new TimeOnly(9, 30));
        Assert.Equal((new DateOnly(2026, 8, 12), new TimeOnly(9, 30)), VisitImportPlanner.CairoParts(instant));
    }

    [Fact]
    public async Task Excel_date_and_time_cells_preview_the_entered_calendar_values()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Visits");
        foreach (var (column, header) in new[] { "Case Number", "Collector", "Visit Date", "Visit Time", "Address", "Notes" }.Select((header, index) => (index + 1, header)))
            sheet.Cell(1, column).Value = header;
        sheet.Cell(2, 1).Value = "CASE-1001";
        sheet.Cell(2, 2).Value = "EMP-1001";
        sheet.Cell(2, 3).Value = new DateTime(2026, 8, 12);
        sheet.Cell(2, 4).Value = new TimeSpan(9, 30, 0);
        sheet.Cell(2, 6).Value = "متابعة";
        sheet.Cell(3, 1).Value = "CASE-1001";
        sheet.Cell(3, 2).Value = "EMP-1001";
        sheet.Cell(3, 3).Value = "10/08/2026";
        sheet.Cell(3, 4).Value = "11:00";
        sheet.Cell(4, 1).Value = "MISSING";
        sheet.Cell(4, 2).Value = "EMP-1001";
        sheet.Cell(4, 3).Value = "15/08/2026";
        sheet.Cell(4, 4).Value = "08:00";
        sheet.Cell(5, 1).Value = "CASE-1001";
        sheet.Cell(5, 2).Value = "EMP-404";
        sheet.Cell(5, 3).Value = "16/08/2026";
        sheet.Cell(5, 4).Value = "08:15";
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        var table = await AttendanceImportParser.ReadTableAsync(stream, ".xlsx", "Visits", 1, 2, CancellationToken.None);
        var existing = new HashSet<(Guid, DateOnly, TimeOnly)> { (CaseId, new DateOnly(2026, 8, 12), new TimeOnly(9, 30)) };
        var rows = table.Rows.Select((cells, index) => VisitImportPlanner.Plan(index + 2, new Dictionary<string, string>
        {
            ["CaseNumber"] = cells.ElementAtOrDefault(0) ?? "",
            ["Collector"] = cells.ElementAtOrDefault(1) ?? "",
            ["VisitDate"] = cells.ElementAtOrDefault(2) ?? "",
            ["VisitTime"] = cells.ElementAtOrDefault(3) ?? "",
            ["Address"] = cells.ElementAtOrDefault(4) ?? "",
            ["Notes"] = cells.ElementAtOrDefault(5) ?? ""
        }, Cases(), Collectors(), Manager, existing, [])).ToArray();

        Assert.Equal("2026-08-12", rows[0].VisitDate);
        Assert.Equal("09:30", rows[0].VisitTime);
        Assert.Equal("Error", rows[0].Status);
        Assert.Contains("A visit is already scheduled for this case at the same date and time.", rows[0].Errors);
        Assert.Equal("Ready", rows[1].Status);
        Assert.Equal("2026-08-10", rows[1].VisitDate);
        Assert.Equal("11:00", rows[1].VisitTime);
        Assert.Equal(new DateOnly(2026, 8, 10), VisitImportPlanner.CairoParts(rows[1].Request!.ScheduledAt).Date);
        Assert.Equal("Error", rows[2].Status);
        Assert.Contains("Case number was not found for this bank or your access.", rows[2].Errors);
        Assert.Equal("Error", rows[3].Status);
        Assert.Contains("The selected collector is outside your authorized scope.", rows[3].Errors);
    }

    [Fact]
    public void Collector_cannot_schedule_another_collector_or_an_unassigned_case()
    {
        var otherCase = new Dictionary<string, VisitImportPlanner.CaseMatch[]>
        {
            ["case-2002"] = [new VisitImportPlanner.CaseMatch(Guid.NewGuid(), "CASE-2002", "Other", "Address", OtherId, "Other")]
        };
        var denied = VisitImportPlanner.Plan(2, Row("CASE-1001", "EMP-404", "12/08/2026", "09:00"), Cases(), Collectors(), Collector, [], []);
        Assert.Contains("Collectors can only create visits for themselves.", denied.Errors);
        var unassigned = VisitImportPlanner.Plan(3, Row("CASE-2002", "EMP-1001", "12/08/2026", "09:00"), otherCase, Collectors(), Collector, [], []);
        Assert.Contains("The case is not assigned to the authenticated collector.", unassigned.Errors);
        var own = VisitImportPlanner.Plan(4, Row("CASE-1001", "", "12/08/2026", "09:00"), Cases(), Collectors(), Collector, [], []);
        Assert.Equal("Ready", own.Status);
        Assert.Equal(CollectorId, own.Request!.AssignedCollectorId);
    }

    [Fact]
    public void Duplicate_rows_in_the_file_are_rejected_and_a_blank_address_uses_the_case_address()
    {
        var batch = new HashSet<(Guid, DateOnly, TimeOnly)>();
        var first = VisitImportPlanner.Plan(2, Row("CASE-1001", "Nada", "12/08/2026", "09:30", ""), Cases(), Collectors(), Manager, [], batch);
        var second = VisitImportPlanner.Plan(3, Row("CASE-1001", "nada", "12/08/2026", "09:30", ""), Cases(), Collectors(), Manager, [], batch);
        Assert.Equal("Ready", first.Status);
        Assert.Equal("12 Nile Street", first.Address);
        Assert.Equal("Error", second.Status);
        Assert.Contains("A visit is already scheduled for this case at the same date and time.", second.Errors);
    }

    [Fact]
    public void Template_uses_only_the_visit_columns()
    {
        using var workbook = new XLWorkbook(new MemoryStream(VisitImportWorkbookBuilder.Build()));
        var sheet = workbook.Worksheet("Visits");
        Assert.Equal(["Case Number", "Collector", "Visit Date", "Visit Time", "Address", "Notes"], sheet.Row(1).CellsUsed().Select(cell => cell.GetString()).ToArray());
        Assert.Equal("12/08/2026", sheet.Cell(2, 3).GetString());
        Assert.Equal("@", sheet.Cell(2, 3).Style.NumberFormat.Format);
        Assert.Equal("@", sheet.Cell(2, 4).Style.NumberFormat.Format);
    }

    private static DateOnly ParseDate(string value)
    {
        Assert.True(VisitImportPlanner.TryDate(value, out var date));
        return date;
    }

    private static Dictionary<string, string> Row(string caseNumber, string collector, string date, string time, string address = "12 Nile Street") => new()
    {
        ["CaseNumber"] = caseNumber,
        ["Collector"] = collector,
        ["VisitDate"] = date,
        ["VisitTime"] = time,
        ["Address"] = address,
        ["Notes"] = "note"
    };

    private static Dictionary<string, VisitImportPlanner.CaseMatch[]> Cases() => new()
    {
        ["case-1001"] = [new VisitImportPlanner.CaseMatch(CaseId, "CASE-1001", "Customer", "12 Nile Street", CollectorId, "Nada")]
    };

    private static CollectorIdentityCandidate[] Collectors() => [Person(CollectorId, "Nada", "nada", "EMP-1001"), Person(OtherId, "Other", "other", "EMP-2002")];

    private static CollectorIdentityCandidate Person(Guid id, string name, string username, string number) =>
        new(id, name, username, $"{username}@mis.local", number, name, name, name);
}
