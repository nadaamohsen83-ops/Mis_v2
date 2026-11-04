using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MIS.Domain.Entities;

namespace MIS.Infrastructure.Persistence.Configurations;

public sealed class LegalClientAssignmentConfiguration : IEntityTypeConfiguration<LegalClientAssignment>
{
    public void Configure(EntityTypeBuilder<LegalClientAssignment> builder)
    {
        builder.ToTable("LegalClientAssignments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasMaxLength(24).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.UpdatedByUser).WithMany().HasForeignKey(x => x.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.CustomerId).IsUnique();
        builder.HasIndex(x => x.UpdatedAt);
    }
}

public sealed class LegalClientDocumentConfiguration : IEntityTypeConfiguration<LegalClientDocument>
{
    public void Configure(EntityTypeBuilder<LegalClientDocument> builder)
    {
        builder.ToTable("LegalClientDocuments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.OriginalFileName).HasMaxLength(260).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(120).IsRequired();
        builder.Property(x => x.FileHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.StorageKey).HasMaxLength(1024).IsRequired();
        builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.UploadedByUser).WithMany().HasForeignKey(x => x.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.CustomerId, x.UploadedAt });
    }
}
