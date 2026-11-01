using System.Globalization;
using System.Text;
using MIS.Application.DTOs.Collections;
using MIS.Domain.Hr;

namespace MIS.Infrastructure.Services;

internal static class VisitImportPlanner
{
    internal static readonly string[] Fields = ["CaseNumber", "Collector", "VisitDate", "VisitTime", "Address", "Notes"];
    internal sealed record CaseMatch(Guid Id, string CaseNumber, string CustomerName, string? Address, Guid? AssignedCollectorId, string? AssignedCollectorName);
    internal sealed record Actor(bool Manager, Guid UserId, CollectorIdentityCandidate Self);

    internal static VisitImportRow Plan(
        int row,
        IReadOnlyDictionary<string, string> values,
        IReadOnlyDictionary<string, CaseMatch[]> cases,
        IReadOnlyCollection<CollectorIdentityCandidate> collectors,
        Actor actor,
        HashSet<(Guid CaseId, DateOnly Date, TimeOnly Time)> existing,
        HashSet<(Guid CaseId, DateOnly Date, TimeOnly Time)> batch)
    {
        var errors = new List<string>();
        var caseText = Cell(values, "CaseNumber");
        var collectorText = Cell(values, "Collector");
        var dateText = Cell(values, "VisitDate");
        var timeText = Cell(values, "VisitTime");
        var addressText = Cell(values, "Address");
        var notes = Cell(values, "Notes");
        string? customer = null;
        string? collectorName = collectorText.Length == 0 ? null : collectorText;
        string? dateDisplay = null;
        string? timeDisplay = null;
        string? addressDisplay = addressText.Length == 0 ? null : addressText;
        CreateBankVisitRequest? request = null;

        CaseMatch? item = null;
        var caseKey = Key(caseText);
        if (caseKey.Length == 0) errors.Add("Case number is required.");
        else if (!cases.TryGetValue(caseKey, out var matches) || matches.Length == 0) errors.Add("Case number was not found for this bank or your access.");
        else if (matches.Length > 1) errors.Add("Case number matches more than one case.");
        else
        {
            item = matches[0];
            customer = item.CustomerName;
        }

        DateOnly date = default;
        var dateOk = false;
        if (dateText.Length == 0) errors.Add("Visit date is required.");
        else if (!TryDate(dateText, out date)) errors.Add("Visit date is invalid.");
        else
        {
            dateOk = true;
            dateDisplay = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        TimeOnly time = default;
        var timeOk = false;
        if (timeText.Length == 0) errors.Add("Visit time is required.");
        else if (!TryTime(timeText, out time)) errors.Add("Visit time is invalid.");
        else
        {
            timeOk = true;
            time = new TimeOnly(time.Hour, time.Minute);
            timeDisplay = time.ToString("HH:mm", CultureInfo.InvariantCulture);
        }

        if (notes.Length > 3000) errors.Add("Notes cannot exceed 3000 characters.");
        if (addressText.Length > 600) errors.Add("Address cannot exceed 600 characters.");

        Guid? collectorId = null;
        if (actor.Manager)
        {
            if (collectorText.Length == 0)
            {
                if (item?.AssignedCollectorId is not Guid assigned) errors.Add("Select an authorized collector for this visit.");
                else if (collectors.FirstOrDefault(candidate => candidate.UserId == assigned) is not { } known)
                    errors.Add("The selected collector is outside your authorized scope.");
                else
                {
                    collectorId = known.UserId;
                    collectorName = known.FullName;
                }
            }
            else
            {
                var match = CollectorIdentity.Match(collectors, collectorText);
                if (match.Kind == CollectorMatchKind.Ambiguous) errors.Add("Ambiguous collector.");
                else if (match.Kind != CollectorMatchKind.Unique || match.UserId is not Guid id)
                    errors.Add("The selected collector is outside your authorized scope.");
                else
                {
                    collectorId = id;
                    collectorName = collectors.First(candidate => candidate.UserId == id).FullName;
                }
            }
        }
        else
        {
            if (collectorText.Length > 0 && CollectorIdentity.Match([actor.Self], collectorText).UserId != actor.UserId)
                errors.Add("Collectors can only create visits for themselves.");
            if (item is not null && item.AssignedCollectorId != actor.UserId)
                errors.Add("The case is not assigned to the authenticated collector.");
            collectorId = actor.UserId;
            collectorName = actor.Self.FullName;
        }

        var caseAddress = Clean(item?.Address);
        if (!actor.Manager && addressText.Length > 0 && !string.Equals(Clean(addressText), caseAddress, StringComparison.Ordinal))
            errors.Add("Collectors cannot correct the case address while creating a visit.");
        var address = actor.Manager ? Clean(addressText) ?? caseAddress : caseAddress;
        if (item is not null && string.IsNullOrWhiteSpace(address)) errors.Add("The selected case has no visit address.");
        else if (!string.IsNullOrWhiteSpace(address)) addressDisplay = address;

        if (errors.Count == 0 && item is not null && collectorId.HasValue && dateOk && timeOk && !string.IsNullOrWhiteSpace(address))
        {
            var key = (item.Id, date, time);
            if (existing.Contains(key) || !batch.Add(key)) errors.Add("A visit is already scheduled for this case at the same date and time.");
            else request = new CreateBankVisitRequest(item.Id, collectorId, ComposeCairo(date, time), address, null, notes.Length == 0 ? null : notes);
        }

        return new VisitImportRow(row, caseText, customer, collectorName, dateDisplay, timeDisplay, addressDisplay, errors.Count == 0 ? "Ready" : "Error", errors, request);
    }

    internal static DateTimeOffset ComposeCairo(DateOnly date, TimeOnly time)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");
        var local = new DateTime(date.Year, date.Month, date.Day, time.Hour, time.Minute, 0, DateTimeKind.Unspecified);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone), TimeSpan.Zero);
    }

    internal static (DateOnly Date, TimeOnly Time) CairoParts(DateTimeOffset instant)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");
        var local = TimeZoneInfo.ConvertTime(instant, zone);
        return (DateOnly.FromDateTime(local.DateTime), new TimeOnly(local.Hour, local.Minute));
    }

    internal static string Key(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var text = Digits(value.Normalize(NormalizationForm.FormKC)).Trim();
        return text.ToLowerInvariant();
    }

    internal static bool TryDate(string value, out DateOnly date)
    {
        date = default;
        var text = Digits(value.Normalize(NormalizationForm.FormKC)).Trim().Replace('\u00A0', ' ');
        if (text.Length == 0) return false;
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

    internal static bool TryTime(string value, out TimeOnly time)
    {
        time = default;
        var text = Digits(value.Normalize(NormalizationForm.FormKC)).Trim();
        if (text.Length == 0) return false;
        var culture = CultureInfo.GetCultureInfo("en-US");
        if (TimeOnly.TryParseExact(text, ["H:mm", "HH:mm", "H:mm:ss", "HH:mm:ss", "HH:mm:ss.FFFFFFF", "h:mm tt", "hh:mm tt"], culture, DateTimeStyles.AllowWhiteSpaces, out time)
            || TryClock(text, out time))
        {
            time = SnapMinute(time);
            return true;
        }
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial) && serial is >= 0 and < 1)
        {
            var seconds = (int)Math.Round(serial * 86400d, MidpointRounding.AwayFromZero);
            if (seconds >= 86400) seconds = 0;
            time = SnapMinute(TimeOnly.FromTimeSpan(TimeSpan.FromSeconds(seconds)));
            return true;
        }
        return false;
    }

    private static bool TryClock(string text, out TimeOnly time)
    {
        if (DateTime.TryParseExact(text, ["yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm:ss.FFFFFFF", "yyyy-MM-dd HH:mm", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-ddTHH:mm:ss.FFFFFFF"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var native))
        {
            time = TimeOnly.FromDateTime(native);
            return true;
        }
        time = default;
        return false;
    }

    // Excel time cells are a fraction of a day and often land a few ticks before the typed minute.
    private static TimeOnly SnapMinute(TimeOnly time)
    {
        if (time.Second < 59) return new TimeOnly(time.Hour, time.Minute);
        var minutes = time.Hour * 60 + time.Minute + 1;
        return minutes >= 24 * 60 ? new TimeOnly(0, 0) : new TimeOnly(minutes / 60, minutes % 60);
    }

    private static string Cell(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) ? value.Trim() : "";

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Digits(string value) => string.Concat(value.Select(character => character switch
    {
        '٠' or '۰' => '0', '١' or '۱' => '1', '٢' or '۲' => '2', '٣' or '۳' => '3',
        '٤' or '۴' => '4', '٥' or '۵' => '5', '٦' or '۶' => '6', '٧' or '۷' => '7',
        '٨' or '۸' => '8', '٩' or '۹' => '9', _ => character
    }));
}
