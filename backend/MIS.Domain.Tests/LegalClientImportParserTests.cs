using ClosedXML.Excel;
using MIS.Infrastructure.Services;
using Xunit;

namespace MIS.Domain.Tests;

public sealed class LegalClientImportParserTests
{
    [Fact]
    public async Task Parses_the_legal_workbook_headers_and_preserves_original_values()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("ورقة1");
        var headers = new[]
        {
            "#", "Branches", "ID Number", "Installment Unique ID", "Cust Full Name", "serial",
            "تاريخ المحضر", "رقم المحضر", "النيابة", "تاريخ الحكم", "الحكم", "Agency",
        };
        for (var index = 0; index < headers.Length; index++)
            sheet.Cell(1, index + 1).Value = headers[index];

        sheet.Cell(2, 1).Value = 1;
        sheet.Cell(2, 1).Style.NumberFormat.Format = "000";
        sheet.Cell(2, 2).Value = "BANK";
        sheet.Cell(2, 3).Value = string.Empty;
        sheet.Cell(2, 4).Value = "00001234";
        sheet.Cell(2, 5).Value = "عميل تجريبي";
        sheet.Cell(2, 6).Value = 23;
        sheet.Cell(2, 6).Style.NumberFormat.Format = "0000";
        sheet.Cell(2, 7).Value = new DateTime(2023, 8, 10);
        sheet.Cell(2, 7).Style.DateFormat.Format = "dd-MMM-yyyy";
        sheet.Cell(2, 8).Value = "000056";
        sheet.Cell(2, 9).Value = "النيابة";
        sheet.Cell(2, 10).Value = "10-Aug-2023";
        sheet.Cell(2, 11).Value = "حكم تجريبي";
        sheet.Cell(2, 12).Value = "Agency A";

        using var content = new MemoryStream();
        workbook.SaveAs(content);
        content.Position = 0;

        var parsed = await CollectionImportParser.ParseAsync(content, ".xlsx", default);
        var row = Assert.Single(parsed);
        var source = LegalClientService.MapImportRow(row, 1);

        Assert.Equal("عميل تجريبي", source.Name);
        Assert.Null(source.NationalId);
        Assert.Null(source.BankName);
        Assert.Equal("00001234", source.ExternalIdentifier);
        Assert.Equal("BANK", source.OriginalFields!["Branches"]);
        Assert.Equal("Agency A", source.OriginalFields["Agency"]);
        Assert.Equal("001", source.OriginalFields!["#"]);
        Assert.Equal("0023", source.OriginalFields["serial"]);
        Assert.Equal("2023-08-10", source.OriginalFields["تاريخ المحضر"]);
        Assert.Equal("10-Aug-2023", source.OriginalFields["تاريخ الحكم"]);
        Assert.Equal(headers.Length, source.OriginalFields.Count);
    }
}
