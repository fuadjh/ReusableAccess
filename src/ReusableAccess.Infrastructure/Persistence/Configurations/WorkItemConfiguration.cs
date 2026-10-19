using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReusableAccess.Domain.Entities;

namespace ReusableAccess.Infrastructure.Persistence.Configurations;

public sealed class WorkItemConfiguration
    : IEntityTypeConfiguration<WorkItem>
{
    public void Configure(EntityTypeBuilder<WorkItem> builder)
    {
        builder.ToTable("WorkItems");

        builder.HasKey(item => item.Id);

        builder.Property(item => item.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(item => item.Status)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(item => item.ScopeId);

        builder.HasOne<AccessScope>()
            .WithMany()
            .HasForeignKey(item => item.ScopeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}