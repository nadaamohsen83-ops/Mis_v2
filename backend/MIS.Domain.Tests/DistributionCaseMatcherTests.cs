using MIS.Infrastructure.Services;
using Xunit;

namespace MIS.Domain.Tests;

public sealed class DistributionCaseMatcherTests
{
    private static readonly Guid CaseId = Guid.Parse("8d815b92-a9e8-4a7f-9463-4f5c02562201");

    [Fact]
    public void File_case_number_matches_the_stored_account_when_excel_status_differs()
    {
        var cases = new[]
        {
            new DistributionCaseMatcher.Candidate(CaseId, "ADIB-ACT-LOAN-13394", "13394", null, "1019196001026437")
        };

        var hits = DistributionCaseMatcher.Find(cases, "  ١٣٣٩٤.0 ", "1019 1960 0102 6437", null);

        Assert.Equal([CaseId], hits);
    }

    [Fact]
    public void Generated_case_number_suffix_matches_the_file_case_number()
    {
        var cases = new[]
        {
            new DistributionCaseMatcher.Candidate(CaseId, "ADIB-ACT-LOAN-8137", "8137", null, null)
        };

        Assert.Equal([CaseId], DistributionCaseMatcher.Find(cases, "8137", null, null));
    }

    [Fact]
    public void Missing_case_is_not_invented_and_empty_status_is_not_an_identifier()
    {
        var cases = new[]
        {
            new DistributionCaseMatcher.Candidate(CaseId, "ADIB-ACT-LOAN-13394", "13394", null, null)
        };

        Assert.Empty(DistributionCaseMatcher.Find(cases, "99999", null, null));
        Assert.False(DistributionCaseMatcher.HasIdentifier("   ", null, null));
    }

    [Fact]
    public void Two_cases_with_the_same_identifier_stay_ambiguous()
    {
        var other = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var cases = new[]
        {
            new DistributionCaseMatcher.Candidate(CaseId, "ADIB-ACT-LOAN-13394", "13394", null, null),
            new DistributionCaseMatcher.Candidate(other, "ADIB-ACT-VISA-13394", "13394", null, null)
        };

        Assert.Equal(2, DistributionCaseMatcher.Find(cases, "13394", null, null).Count);
    }
}
