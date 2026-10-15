using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core mapping for the TicketEscalation entity.
/// Configures keys, lengths, foreign keys, delete behaviors, and index.
/// </summary>
public sealed class TicketEscalationConfiguration : IEntityTypeConfiguration<TicketEscalation>
{
    public void Configure(EntityTypeBuilder<TicketEscalation> builder)
    {
        builder.ToTable("TicketEscalations");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.TicketId).IsRequired();

        builder.Property(e => e.FromPriority).HasConversion<int>().IsRequired();
        builder.Property(e => e.ToPriority).HasConversion<int>().IsRequired();

        builder.Property(e => e.Reason).HasMaxLength(500).IsRequired();
        builder.Property(e => e.EscalatedBy).HasMaxLength(100).IsRequired();

        builder.Property(e => e.FromDueAtUtc).HasColumnType("datetime2(3)");
        builder.Property(e => e.ToDueAtUtc).HasColumnType("datetime2(3)");
        builder.Property(e => e.EscalatedAtUtc).HasColumnType("datetime2(3)").IsRequired();

        // Foreign keys
        builder.HasOne<Ticket>()
            .WithMany()
            .HasForeignKey(e => e.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Agent>()
            .WithMany()
            .HasForeignKey(e => e.FromAgentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Agent>()
            .WithMany()
            .HasForeignKey(e => e.ToAgentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Index rationale: Escalation history is queried per ticket, ordered by newest first.
        builder.HasIndex(e => new { e.TicketId, e.EscalatedAtUtc })
            .IsDescending(false, true)
            .HasDatabaseName("IX_TicketEscalations_TicketId_EscalatedAtUtc");
    }
}
