using System.Globalization;
using System.Text;
using MIS.Application.Common;
using MIS.Application.DTOs.Hr;

namespace MIS.Infrastructure.Services;

internal static class ExcuseImportMapper
{
    internal static readonly string[] Fields = ["EmployeeNumber", "ExcuseType", "Date", "FromTime", "ToTime", "Notes"];
    private static readonly string[] AllowedTypes = ["PersonalExcuse", "MedicalExcuse", "OfficialMission", "LateArrivalExcuse", "EarlyLeaveExcuse", "Other"];
    private static readonly Dictionary<string, string> TypeByKey = BuildTypes();

    internal sealed record EmployeeMatch(Guid Id, string EmployeeNumber, string FullName, bool IsActive, bool IsArchived, DateOnly? HireDate, DateOnly? TerminationDate);

    internal static Dictionary<string, EmployeeMatch[]> Index(IReadOnlyCollection<EmployeeMatch> employees) =>
        employees.GroupBy(employee => NumberKey(employee.EmployeeNumber))
            .Where(group => group.Key.Length > 0)
            .ToDictionary(group => group.Key, group => group.ToArray());

    internal static ExcuseImportRow Map(
        int row,
        IReadOnlyDictionary<string, string> values,
        string? dateFormat,
        IReadOnlyDictionary<string, EmployeeMatch[]> employees,
        HashSet<(Guid EmployeeId, DateOnly Date, TimeOnly? From, TimeOnly? To)> existing,
        HashSet<(Guid EmployeeId, DateOnly Date, TimeOnly? From, TimeOnly? To)> batch)
    {
        var errors = new List<string>();
        var number = Cell(values, "EmployeeNumber");
        var typeText = Cell(values, "ExcuseType");
        var dateText = Cell(values, "Date");
        var fromText = Cell(values, "FromTime");
        var toText = Cell(values, "ToTime");
        var notes = Cell(values, "Notes");
        string employeeName = "";
        string? dateDisplay = dateText.Length == 0 ? null : dateText;
        string? fromDisplay = fromText.Length == 0 ? null : fromText;
        string? toDisplay = toText.Length == 0 ? null : toText;
        ExcuseImportRecord? record = null;

        EmployeeMatch? employee = null;
        var numberKey = NumberKey(number);
        if (numberKey.Length == 0) errors.Add("Employee number is required.");
        else if (!employees.TryGetValue(numberKey, out var matches) || matches.Length == 0) errors.Add("Employee number was not found.");
        else if (matches.Length > 1) errors.Add("Employee number matches more than one employee.");
        else
        {
            employee = matches[0];
            employeeName = employee.FullName;
            if (!employee.IsActive || employee.IsArchived) errors.Add("Select an active employee for a manual excuse.");
        }

        string? type = null;
        if (typeText.Length == 0) errors.Add("Excuse type is required.");
        else if (!TypeByKey.TryGetValue(TypeKey(typeText), out type) || !AllowedTypes.Contains(type)) errors.Add("Excuse type is invalid.");

        DateOnly date = default;
        var dateOk = false;
        if (dateText.Length == 0) errors.Add("Excuse date is required.");
        else if (!TryDate(dateText, dateFormat, out date)) errors.Add("Excuse date is invalid.");
        else
        {
            dateOk = true;
            dateDisplay = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (employee?.HireDate is DateOnly hired && date < hired) errors.Add("Excuse date cannot precede employment.");
            if (employee?.TerminationDate is DateOnly ended && date > ended) errors.Add("Excuse date is after the employee left service.");
        }

        var fullDay = fromText.Length == 0 && toText.Length == 0;
        TimeOnly? from = null;
        TimeOnly? to = null;
        if (!fullDay && (fromText.Length == 0 || toText.Length == 0))
            errors.Add("From Time and To Time must both be entered, or both left empty for a full day.");
        else if (!fullDay)
        {
            if (!TryTime(fromText, out var parsedFrom)) errors.Add("Excuse time is invalid.");
            else { from = parsedFrom; fromDisplay = FormatTime(parsedFrom); }
            if (!TryTime(toText, out var parsedTo)) errors.Add("Excuse time is invalid.");
            else { to = parsedTo; toDisplay = FormatTime(parsedTo); }
            if (from.HasValue && to.HasValue && to <= from) errors.Add("End time must be after start time.");
        }

        if (notes.Length == 0) errors.Add("Notes are required because they are saved as the excuse reason.");
        else if (notes.Length > 1000) errors.Add("Notes are longer than 1000 characters.");

        if (errors.Count == 0 && employee is not null && type is not null && dateOk)
        {
            var key = (employee.Id, date, fullDay ? null : from, fullDay ? null : to);
            if (existing.Contains(key)) errors.Add("An active manual excuse already exists for this employee, date and period.");
            else if (!batch.Add(key)) errors.Add("This excuse is duplicated in the file.");
            else record = new ExcuseImportRecord(employee.Id, type, date, fullDay, fullDay ? null : from, fullDay ? null : to, notes);
        }

        return new ExcuseImportRow(row, number, employeeName, typeText, dateDisplay, fromDisplay, toDisplay, errors.Count == 0 ? "Ready" : "Error", errors, record);
    }

    internal static string NumberKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var text = Digits(value.Normalize(NormalizationForm.FormKC)).Trim().Replace(" ", "", StringComparison.Ordinal);
        if (text.Length > 2 && text.Contains('.', StringComparison.Ordinal) && text.EndsWith('0') && decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var number) && number == decimal.Truncate(number))
            text = decimal.Truncate(number).ToString(CultureInfo.InvariantCulture);
        return text.ToLowerInvariant();
    }

    internal static bool TryDate(string value, string? format, out DateOnly date)
    {
        date = default;
        var text = Digits(value.Normalize(NormalizationForm.FormKC)).Trim().Replace('\u00A0', ' ');
        if (text.Length == 0) return false;
        if (!string.IsNullOrWhiteSpace(format) && !IsMonthFirst(format))
        {
            try
            {
                if (DateOnly.TryParseExact(text, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out date)) return true;
            }
            catch (FormatException)
            {
                throw new HrValidationException("Invalid date format.");
            }
        }

        if (text.Length >= 10 && text[4] == '-' && text[7] == '-'
            && DateOnly.TryParseExact(text[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            return true;

        if (DateOnly.TryParseExact(text, ["dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy", "dd.MM.yyyy", "d.M.yyyy"], CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            return true;

        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial) && serial is >= 1 and <= 2958465)
        {
            try
            {
                date = DateOnly.FromDateTime(DateTime.FromOADate(serial));
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        return false;
    }

    private static bool IsMonthFirst(string format)
    {
        var token = format.Trim();
        return token.StartsWith("MM/dd", StringComparison.OrdinalIgnoreCase)
            || token.StartsWith("M/d", StringComparison.OrdinalIgnoreCase)
            || token.StartsWith("MM-dd", StringComparison.OrdinalIgnoreCase)
            || token.StartsWith("M-d", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool TryTime(string value, out TimeOnly time)
    {
        time = default;
        var text = Digits(value.Normalize(NormalizationForm.FormKC)).Trim();
        if (text.Length == 0) return false;
        var culture = CultureInfo.GetCultureInfo("en-US");
        var formats = new[] { "H:mm", "HH:mm", "H:mm:ss", "HH:mm:ss", "HH:mm:ss.FFFFFFF", "h:mm tt", "hh:mm tt", "h:mm:ss tt", "hh:mm:ss tt" };
        if (TimeOnly.TryParseExact(text, formats, culture, DateTimeStyles.AllowWhiteSpaces, out time)) return true;
        if (DateTime.TryParseExact(text, ["yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm:ss.FFFFFFF", "yyyy-MM-dd HH:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var native))
        {
            time = TimeOnly.FromDateTime(native);
            return true;
        }
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial) && serial is >= 0 and < 1)
        {
            var seconds = (int)Math.Round(serial * 86400d, MidpointRounding.AwayFromZero);
            if (seconds >= 86400) seconds = 86399;
            time = TimeOnly.FromTimeSpan(TimeSpan.FromSeconds(seconds));
            return true;
        }
        return false;
    }

    private static string FormatTime(TimeOnly time) => time.Second == 0 ? time.ToString("HH:mm", CultureInfo.InvariantCulture) : time.ToString("HH:mm:ss", CultureInfo.InvariantCulture);

    private static string Cell(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) ? value.Trim() : "";

    private static string Digits(string value) => string.Concat(value.Select(character => character switch
    {
        '٠' or '۰' => '0', '١' or '۱' => '1', '٢' or '۲' => '2', '٣' or '۳' => '3',
        '٤' or '۴' => '4', '٥' or '۵' => '5', '٦' or '۶' => '6', '٧' or '۷' => '7',
        '٨' or '۸' => '8', '٩' or '۹' => '9', _ => character
    }));

    private static string TypeKey(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Normalize(NormalizationForm.FormKC))
        {
            if (char.IsWhiteSpace(character) || character == '\u0640') continue;
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            builder.Append(char.ToLowerInvariant(character switch
            {
                'أ' or 'إ' or 'آ' or 'ٱ' => 'ا',
                'ة' => 'ه',
                'ى' or 'ئ' => 'ي',
                'ؤ' => 'و',
                _ => character
            }));
        }
        return builder.ToString();
    }

    private static Dictionary<string, string> BuildTypes()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        void Add(string code, params string[] names)
        {
            foreach (var name in names.Prepend(code)) map[TypeKey(name)] = code;
        }
        Add("PersonalExcuse", "Personal Permission", "Personal Excuse", "إذن شخصي", "اذن شخصي", "عذر شخصي");
        Add("MedicalExcuse", "Medical Excuse", "عذر طبي");
        Add("OfficialMission", "Official Mission", "مأمورية عمل", "مأموريه عمل", "مأمورية");
        Add("LateArrivalExcuse", "Late Arrival Excuse", "Late Arrival", "عذر تأخير", "عذر تاخير");
        Add("EarlyLeaveExcuse", "Early Leave Excuse", "Early Leave", "عذر انصراف مبكر");
        Add("Other", "أخرى", "اخرى");
        return map;
    }
}
