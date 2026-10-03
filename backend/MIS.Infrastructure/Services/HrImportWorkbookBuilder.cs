using ClosedXML.Excel;

namespace MIS.Infrastructure.Services;

internal static class HrImportWorkbookBuilder
{
    public const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static byte[] BuildSocialInsurance()
    {
        using var workbook = new XLWorkbook();
        var sheet = AddSheet(workbook, "Insurance",
            "Employee Number", "Employee Name", "National ID", "Social Insurance Number",
            "Insurance Start Date", "Insurance End Date", "Insurable Salary", "Insurance Status",
            "Insurance Office", "Reference Number", "Notes");
        sheet.Range("E2:F2001").Style.DateFormat.Format = "yyyy-mm-dd";
        sheet.Range("G2:G2001").Style.NumberFormat.Format = "#,##0.00";
        sheet.Column(2).Width = 28;
        sheet.Column(4).Width = 24;
        sheet.Column(9).Width = 22;
        sheet.Column(11).Width = 32;
        AddInstructions(workbook,
            ("How to use", "طريقة الاستخدام"),
            ("Enter one insurance record per row in the Insurance sheet. Do not rename the headers.", "أدخل سجل تأمين واحد في كل صف داخل ورقة Insurance ولا تغيّر أسماء الأعمدة."),
            ("Match the employee by Employee Number or National ID. Names are verification only.", "المطابقة برقم الموظف أو الرقم القومي. الاسم للتحقق فقط."),
            ("Required: Social Insurance Number, Insurable Salary, and Insurance Status.", "إلزامي: الرقم التأميني والأجر التأميني وحالة التأمين."),
            ("Insurance Status: Insured, NotInsured, Suspended, or Ended. Ended requires Insurance End Date.", "حالة التأمين: Insured أو NotInsured أو Suspended أو Ended. الحالة Ended تحتاج تاريخ نهاية."),
            ("Use yyyy-mm-dd for dates and enter salary without thousands separators.", "استخدم yyyy-mm-dd للتواريخ، واكتب الأجر دون فواصل آلاف."),
            ("Existing active insurance records are skipped and never overwritten.", "سجل التأمين النشط الموجود يتم تجاوزه ولا يُستبدل."));
        return Save(workbook);
    }

    public static byte[] BuildAbsence()
    {
        using var workbook = new XLWorkbook();
        var sheet = AddSheet(workbook, "Absences",
            "Employee Number", "Employee Name", "National ID", "Mobile Number",
            "Absence Date", "Absence Type", "Reason", "Notes", "Status");
        sheet.Range("E2:E2001").Style.DateFormat.Format = "yyyy-mm-dd";
        sheet.Column(2).Width = 28;
        sheet.Column(7).Width = 28;
        sheet.Column(8).Width = 32;
        AddInstructions(workbook,
            ("How to use", "طريقة الاستخدام"),
            ("Enter one absence per row in the Absences sheet. Do not rename the headers.", "أدخل غيابًا واحدًا في كل صف داخل ورقة Absences ولا تغيّر أسماء الأعمدة."),
            ("Match the employee by Employee Number, National ID, or Mobile Number.", "المطابقة برقم الموظف أو الرقم القومي أو رقم الموبايل."),
            ("Required: Absence Date. Use yyyy-mm-dd.", "إلزامي: تاريخ الغياب. استخدم yyyy-mm-dd."),
            ("Absence Type is optional and should be Absent. Status: Pending, Excused, or Unexcused.", "نوع الغياب اختياري ويجب أن يكون Absent. الحالة: Pending أو Excused أو Unexcused."),
            ("Duplicate absences and dates that conflict with leave, excuse, or attendance are skipped.", "الغيابات المكررة والتعارض مع الإجازة أو العذر أو الحضور يتم تجاوزها."));
        return Save(workbook);
    }

    public static byte[] BuildExcuse()
    {
        using var workbook = new XLWorkbook();
        var sheet = AddSheet(workbook, "Excuses",
            "Employee Number", "Excuse Type", "Date", "From Time", "To Time", "Notes");
        sheet.Cell(2, 1).Value = "EMP-1001";
        sheet.Cell(2, 2).Value = "إذن شخصي";
        sheet.Cell(2, 3).Value = "2026-10-03";
        sheet.Cell(2, 4).Value = new TimeSpan(9, 0, 0);
        sheet.Cell(2, 5).Value = new TimeSpan(11, 0, 0);
        sheet.Cell(2, 6).Value = "مراجعة جهة حكومية";
        sheet.Range("C2:C2001").Style.NumberFormat.Format = "@";
        sheet.Range("D2:E2001").Style.NumberFormat.Format = "hh:mm";
        sheet.Column(2).Width = 24;
        sheet.Column(6).Width = 36;
        AddInstructions(workbook,
            ("How to use", "طريقة الاستخدام"),
            ("Enter one excuse per row. Do not rename the headers. The example row can be replaced.", "أدخل عذرًا واحدًا في كل صف ولا تغيّر أسماء الأعمدة. يمكن استبدال صف المثال."),
            ("Employees are matched only by Employee Number. New employees are not created.", "المطابقة برقم الموظف فقط. لن يُنشأ موظف جديد."),
            ("Excuse Type must already exist: PersonalExcuse, MedicalExcuse, OfficialMission, LateArrivalExcuse, EarlyLeaveExcuse, Other, or the Arabic name.", "نوع العذر يجب أن يكون موجودًا: إذن شخصي، عذر طبي، مأمورية عمل، عذر تأخير، عذر انصراف مبكر، أخرى، أو الكود الإنجليزي."),
            ("Date: type DD/MM/YYYY or yyyy-mm-dd. The column is text so Excel keeps the calendar day you type. Leave both times empty for a full day, or enter both with To Time after From Time.", "التاريخ: اكتب DD/MM/YYYY أو yyyy-mm-dd. العمود نصي حتى يحتفظ Excel باليوم الذي تكتبه. اترك الوقتين فارغين ليوم كامل، أو أدخلهما معًا بحيث تكون النهاية بعد البداية."),
            ("Notes are required and are saved as the excuse reason. Imported rows stay pending approval.", "الملاحظات إلزامية وتُحفظ كسبب العذر. الصفوف المستوردة تبقى بانتظار الموافقة."));
        return Save(workbook);
    }

    public static byte[] BuildAttendance()
    {
        using var workbook = new XLWorkbook();
        var attendance = AddSheet(workbook, "Attendance",
            "Employee Number", "Employee Name", "Attendance Date", "Check In", "Check Out");
        attendance.Range("C2:C2001").Style.DateFormat.Format = "yyyy-mm-dd";
        attendance.Range("D2:E2001").Style.NumberFormat.Format = "hh:mm";
        attendance.Column(2).Width = 28;

        var fingerprint = AddSheet(workbook, "Fingerprint",
            "Name", "No.", "Date/Time", "Date", "Time", "AM-PM");
        fingerprint.Column(1).Width = 28;
        fingerprint.Column(3).Width = 22;
        fingerprint.Column(4).Width = 14;

        AddInstructions(workbook,
            ("How to use", "طريقة الاستخدام"),
            ("Use the Attendance sheet for one row per day with check-in and check-out times.", "استخدم ورقة Attendance لصف واحد لكل يوم مع وقت الحضور والانصراف."),
            ("Use the Fingerprint sheet for device punch exports (Name, No., Date, Time, AM-PM).", "استخدم ورقة Fingerprint لتصدير جهاز البصمة (Name و No. و Date و Time و AM-PM)."),
            ("Do not rename the headers. After upload, confirm the detected columns then preview.", "لا تغيّر أسماء الأعمدة. بعد الرفع أكد الأعمدة المكتشفة ثم عاين."),
            ("Attendance Date: yyyy-mm-dd. Check In / Check Out: HH:mm (24-hour) or h:mm:ss with AM-PM on the fingerprint sheet.", "تاريخ الحضور: yyyy-mm-dd. الحضور والانصراف: HH:mm أو h:mm:ss مع AM-PM في ورقة البصمة."),
            ("Attendance is not saved until mapping, preview, and confirmation are complete.", "لن يُحفظ الحضور قبل اكتمال تعيين الأعمدة والمعاينة والتأكيد."));
        return Save(workbook);
    }

    public static IXLWorksheet AddSheet(XLWorkbook workbook, string name, params string[] headers)
    {
        var sheet = workbook.Worksheets.Add(name);
        for (var index = 0; index < headers.Length; index++)
        {
            var cell = sheet.Cell(1, index + 1);
            cell.Value = headers[index];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#0B638F");
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        sheet.SheetView.FreezeRows(1);
        if (headers.Length > 0)
            sheet.Range(1, 1, 2001, headers.Length).SetAutoFilter();
        sheet.Columns().AdjustToContents();
        return sheet;
    }

    public static void AddInstructions(XLWorkbook workbook, params (string En, string Ar)[] rows)
    {
        var sheet = workbook.Worksheets.Add("Instructions - تعليمات");
        sheet.Cell(1, 1).Value = "English";
        sheet.Cell(1, 2).Value = "العربية";
        sheet.Range(1, 1, 1, 2).Style.Font.Bold = true;
        sheet.Range(1, 1, 1, 2).Style.Fill.BackgroundColor = XLColor.FromHtml("#E7F3FA");
        for (var index = 0; index < rows.Length; index++)
        {
            sheet.Cell(index + 2, 1).Value = rows[index].En;
            sheet.Cell(index + 2, 2).Value = rows[index].Ar;
        }

        sheet.Columns(1, 2).Width = 70;
        sheet.Columns(1, 2).Style.Alignment.WrapText = true;
    }

    public static byte[] Save(XLWorkbook workbook)
    {
        using var output = new MemoryStream();
        workbook.SaveAs(output);
        return output.ToArray();
    }
}
