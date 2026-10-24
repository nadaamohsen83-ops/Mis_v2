using ClosedXML.Excel;
using MIS.Infrastructure.Services;
using Xunit;

namespace MIS.Domain.Tests;

public sealed class LegalCaseImportTests
{
    [Fact]
    public void Legal_case_import_template_contains_supported_case_and_customer_fields()
    {
        var bytes = new LegalCaseImportService(null!, null!).BuildTemplate();
        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var headers = workbook.Worksheet("Legal Cases").Row(1).CellsUsed().Select(x => x.GetString()).ToArray();

        Assert.Equal(
        [
            "Customer Code", "Customer Name", "National ID", "Mobile Number", "Collection Case Number",
            "Account Number", "Entity / Bank", "Portfolio", "Legal Case Number", "Legal Stage", "Court",
            "Next Session Date", "Lawyer / Responsible Person", "Notes",
        ],
        headers);
    }
}
