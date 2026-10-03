using ClosedXML.Excel;
using MIS.Infrastructure.Services;
using Xunit;

namespace MIS.Domain.Tests;

public sealed class HrImportTemplateTests
{
    [Fact]
    public void Social_insurance_template_headers_match_import_fields()
    {
        using var workbook = new XLWorkbook(new MemoryStream(HrImportWorkbookBuilder.BuildSocialInsurance()));
        Assert.Equal(
            ["Employee Number", "Employee Name", "National ID", "Social Insurance Number",
             "Insurance Start Date", "Insurance End Date", "Insurable Salary", "Insurance Status",
             "Insurance Office", "Reference Number", "Notes"],
            Headers(workbook, "Insurance"));
    }

    [Fact]
    public void Absence_template_headers_match_import_fields()
    {
        using var workbook = new XLWorkbook(new MemoryStream(HrImportWorkbookBuilder.BuildAbsence()));
        Assert.Equal(
            ["Employee Number", "Employee Name", "National ID", "Mobile Number",
             "Absence Date", "Absence Type", "Reason", "Notes", "Status"],
            Headers(workbook, "Absences"));
    }

    [Fact]
    public void Excuse_template_has_headers_and_one_example_row()
    {
        using var workbook = new XLWorkbook(new MemoryStream(HrImportWorkbookBuilder.BuildExcuse()));
        var sheet = workbook.Worksheet("Excuses");
        Assert.Equal(
            ["Employee Number", "Excuse Type", "Date", "From Time", "To Time", "Notes"],
            Headers(workbook, "Excuses"));
        Assert.Equal("EMP-1001", sheet.Cell(2, 1).GetString());
        Assert.Equal("إذن شخصي", sheet.Cell(2, 2).GetString());
        Assert.Equal("2026-10-03", sheet.Cell(2, 3).GetString());
        Assert.Equal("@", sheet.Cell(2, 3).Style.NumberFormat.Format);
        Assert.False(string.IsNullOrWhiteSpace(sheet.Cell(2, 6).GetString()));
    }

    [Fact]
    public void Attendance_template_includes_check_in_out_and_fingerprint_sheets()
    {
        using var workbook = new XLWorkbook(new MemoryStream(HrImportWorkbookBuilder.BuildAttendance()));
        Assert.Equal(
            ["Employee Number", "Employee Name", "Attendance Date", "Check In", "Check Out"],
            Headers(workbook, "Attendance"));
        Assert.Equal(
            ["Name", "No.", "Date/Time", "Date", "Time", "AM-PM"],
            Headers(workbook, "Fingerprint"));
    }

    private static string[] Headers(XLWorkbook workbook, string sheetName) =>
        workbook.Worksheet(sheetName).Row(1).CellsUsed().Select(cell => cell.GetString()).ToArray();
}
