using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReusableAccess.Domain.Entities;
namespace ReusableAccess.Infrastructure.Persistence.Configurations;
public sealed class AuthenticationPolicyConfiguration : IEntityTypeConfiguration<AuthenticationPolicy>
{
    public void Configure(EntityTypeBuilder<AuthenticationPolicy> builder)
    {
        builder.ToTable("AuthenticationPolicies");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Version).IsConcurrencyToken();
        builder.HasData(new AuthenticationPolicy { Id = 1 });
    }
}
