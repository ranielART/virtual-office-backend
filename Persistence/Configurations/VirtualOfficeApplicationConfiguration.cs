using System.Text.Json;
using Domain.Entity.Coworking;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Configurations
{
    public class VirtualOfficeApplicationConfiguration
        : IEntityTypeConfiguration<VirtualOfficeApplication>
    {
        private static readonly JsonSerializerOptions _jsonOptions = new();

        // Value comparer for List<string> so EF Core detects changes correctly
        private static readonly ValueComparer<List<string>> _listComparer = new(
            (a, b) => a != null && b != null && a.SequenceEqual(b),
            c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
            c => c.ToList());

        public void Configure(EntityTypeBuilder<VirtualOfficeApplication> builder)
        {
            builder.HasKey(v => v.Id);

            builder.Property(v => v.BusinessType)
                .HasConversion<string>();

            builder.Property(v => v.Status)
                .HasConversion<string>();

            // Serialize URL lists as JSONB columns with value comparers
            builder.Property(v => v.BusinessDocumentUrls)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, _jsonOptions),
                    v => JsonSerializer.Deserialize<List<string>>(v, _jsonOptions) ?? new List<string>())
                .HasColumnType("jsonb")
                .Metadata.SetValueComparer(_listComparer);

            builder.Property(v => v.DownPaymentProofUrls)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, _jsonOptions),
                    v => JsonSerializer.Deserialize<List<string>>(v, _jsonOptions) ?? new List<string>())
                .HasColumnType("jsonb")
                .Metadata.SetValueComparer(_listComparer);

            builder.HasIndex(v => v.Email);
            builder.HasIndex(v => v.Status);
        }
    }
}
