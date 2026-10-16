using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RutaProducto.Infrastructure.DataProtection;

/// <summary>
/// Platform table (not a module table): it holds the framework's <see cref="DataProtectionKey"/> rows and no business data,
/// so it carries the <c>dp_</c> prefix. Always part of the model; it is only written when <c>DataProtection:Provider=Database</c>.
/// </summary>
internal sealed class DataProtectionKeyConfiguration : IEntityTypeConfiguration<DataProtectionKey>
{
    public const string TableName = "dp_keys";

    public void Configure(EntityTypeBuilder<DataProtectionKey> builder) => builder.ToTable(TableName);
}
