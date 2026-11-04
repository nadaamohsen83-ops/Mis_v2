using MIS.Domain.Entities;
using Xunit;

namespace MIS.Domain.Tests;

public sealed class LegalClientManagementTests
{
    [Fact]
    public void Legal_import_fills_missing_customer_data_without_overwriting_existing_data()
    {
        var customer = new CollectionCustomer(Guid.NewGuid(), "C-1", "الاسم الحالي", "Existing Name", DateTimeOffset.UtcNow);
        customer.ApplyImportedContact(null, null, "12345678901234", "01000000000");
        customer.FillMissingLegalImportDetails("اسم جديد", "New Name", "99999999999999", "01111111111", null, "New Address");

        Assert.Equal("Existing Name", customer.FullNameEnglish);
        Assert.Equal("12345678901234", customer.NationalId);
        Assert.Equal("01000000000", customer.PrimaryPhone);
        Assert.Equal("New Address", customer.AddressEnglish);
    }

    [Fact]
    public void Legal_client_assignment_tracks_status_and_notes_for_the_existing_customer()
    {
        var customerId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var assignment = new LegalClientAssignment(customerId, userId, DateTimeOffset.UtcNow);

        assignment.Update("ON_HOLD", "  Review documents  ", userId, DateTimeOffset.UtcNow.AddMinutes(1));

        Assert.Equal(customerId, assignment.CustomerId);
        Assert.Equal("ON_HOLD", assignment.Status);
        Assert.Equal("Review documents", assignment.Notes);
    }

    [Fact]
    public void Legal_client_document_stores_only_a_safe_file_name()
    {
        var document = new LegalClientDocument(
            Guid.NewGuid(),
            @"C:\client-files\identity.pdf",
            "application/pdf",
            128,
            "hash",
            "legal-client-documents/identity.pdf",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);

        Assert.Equal("identity.pdf", document.OriginalFileName);
    }
}
