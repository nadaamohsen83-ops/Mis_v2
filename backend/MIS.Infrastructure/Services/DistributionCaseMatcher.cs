using System.Text;
using System.Text.RegularExpressions;

namespace MIS.Infrastructure.Services;

internal static partial class DistributionCaseMatcher
{
    internal readonly record struct Candidate(Guid Id, string CaseNumber, string? AccountReference, string? ContractReference, string? CardNumber);

    internal static bool HasIdentifier(string? caseNumber, string? account, string? contract) =>
        Keys(caseNumber, account, contract).Length > 0;

    internal static IReadOnlyCollection<Guid> Find(IReadOnlyCollection<Candidate> cases, string? caseNumber, string? account, string? contract)
    {
        var keys = Keys(caseNumber, account, contract);
        if (keys.Length == 0 || cases.Count == 0) return [];
        return cases.Where(item => keys.Any(key => Hits(item, key))).Select(item => item.Id).Distinct().ToArray();
    }

    internal static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value.Normalize(NormalizationForm.FormKC))
        {
            if (char.IsWhiteSpace(ch)) continue;
            builder.Append(ToAsciiDigit(ch) ?? ch);
        }

        var text = builder.ToString().Trim();
        var decimalNumber = TrailingDecimalZero().Match(text);
        return decimalNumber.Success ? decimalNumber.Groups[1].Value : text;
    }

    private static string[] Keys(string? caseNumber, string? account, string? contract) =>
        new[] { caseNumber, account, contract }
            .Select(Normalize)
            .Where(key => key.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static bool Hits(Candidate item, string key) =>
        Equal(item.CaseNumber, key)
        || Equal(item.AccountReference, key)
        || Equal(item.ContractReference, key)
        || Equal(item.CardNumber, key)
        || CaseNumberSuffix(item.CaseNumber, key);

    private static bool Equal(string? stored, string key)
    {
        var left = Normalize(stored);
        return left.Length > 0 && left.Equals(key, StringComparison.OrdinalIgnoreCase);
    }

    private static bool CaseNumberSuffix(string? caseNumber, string key)
    {
        if (key.Length < 4 || string.IsNullOrWhiteSpace(caseNumber)) return false;
        var separator = caseNumber.LastIndexOf('-');
        if (separator < 0 || separator == caseNumber.Length - 1) return false;
        return Equal(caseNumber[(separator + 1)..], key);
    }

    private static char? ToAsciiDigit(char ch)
    {
        const string arabic = "٠١٢٣٤٥٦٧٨٩";
        const string eastern = "۰۱۲۳۴۵۶۷۸۹";
        var index = arabic.IndexOf(ch);
        if (index < 0) index = eastern.IndexOf(ch);
        return index >= 0 ? (char)('0' + index) : null;
    }

    [GeneratedRegex(@"^(\d+)\.0+$", RegexOptions.CultureInvariant)]
    private static partial Regex TrailingDecimalZero();
}
