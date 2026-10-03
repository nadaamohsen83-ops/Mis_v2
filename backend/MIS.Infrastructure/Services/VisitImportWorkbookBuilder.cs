using ClosedXML.Excel;

namespace MIS.Infrastructure.Services;

internal static class VisitImportWorkbookBuilder
{
    public const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static byte[] Build()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Visits");
        var headers = new[] { "Case Number", "Collector", "Visit Date", "Visit Time", "Address", "Notes" };
        for (var index = 0; index < headers.Length; index++)
        {
            var cell = sheet.Cell(1, index + 1);
            cell.Value = headers[index];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#0B638F");
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }
        sheet.Cell(2, 1).Value = "CASE-1001";
        sheet.Cell(2, 2).Value = "EMP-1001";
        sheet.Cell(2, 3).Value = "12/08/2026";
        sheet.Cell(2, 4).Value = "09:30";
        sheet.Cell(2, 5).Value = "12 Nile Street, Cairo";
        sheet.Cell(2, 6).Value = "Customer follow-up";
        sheet.Range("C2:D2001").Style.NumberFormat.Format = "@";
        sheet.SheetView.FreezeRows(1);
        sheet.Range(1, 1, 2001, headers.Length).SetAutoFilter();
        sheet.Columns().AdjustToContents();
        sheet.Column(5).Width = 36;
        sheet.Column(6).Width = 32;

        var instructions = workbook.Worksheets.Add("Instructions - تعليمات");
        var lines = new (string En, string Ar)[]
        {
            ("How to use", "طريقة الاستخدام"),
            ("Enter one visit per row. Do not rename the headers. The example row can be replaced.", "أدخل زيارة واحدة في كل صف ولا تغيّر أسماء الأعمدة. يمكن استبدال صف المثال."),
            ("Case Number must already exist in this bank. The import does not create cases.", "رقم الحالة يجب أن يكون موجوداً في هذا البنك. الاستيراد لا ينشئ حالات."),
            ("Collector is the employee number, username, or collector name, and must already be allowed to work this case.", "المحصل هو رقم الموظف أو اسم المستخدم أو اسم المحصل، ويجب أن يكون مصرحاً له بالعمل على هذه الحالة."),
            ("Visit Date: DD/MM/YYYY or yyyy-mm-dd. Visit Time: HH:mm. These columns are text so Excel keeps the calendar day and time you type.", "تاريخ الزيارة: DD/MM/YYYY أو yyyy-mm-dd. وقت الزيارة: HH:mm. العمودان نصيان حتى يحتفظ Excel باليوم والوقت اللذين تكتبهما."),
            ("Address can be left empty to use the case address. Notes are optional. Imported visits are created with the same rules as إنشاء زيارة.", "يمكن ترك العنوان فارغاً لاستخدام عنوان الحالة. الملاحظات اختيارية. الزيارات المستوردة تُنشأ بنفس قواعد إنشاء زيارة.")
        };
        instructions.Cell(1, 1).Value = "English";
        instructions.Cell(1, 2).Value = "العربية";
        instructions.Cell(1, 1).Style.Font.Bold = true;
        instructions.Cell(1, 2).Style.Font.Bold = true;
        for (var index = 0; index < lines.Length; index++)
        {
            instructions.Cell(index + 2, 1).Value = lines[index].En;
            instructions.Cell(index + 2, 2).Value = lines[index].Ar;
        }
        instructions.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
