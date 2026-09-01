using FiapGames.Orders.Api.Domain;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace FiapGames.Orders.Api.Infrastructure.Persistence;

public sealed class OrdersDbContext : DbContext
{
    public const string Schema = "orders";

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    public DbSet<OrderEvent> OrderEvents => Set<OrderEvent>();

    public OrdersDbContext(DbContextOptions<OrdersDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Order>(builder =>
        {
            builder.ToTable("orders");
            builder.HasKey(o => o.Id);
            builder.Property(o => o.UserId).IsRequired();
            builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);

            builder.HasMany(o => o.Items)
                .WithOne()
                .HasForeignKey(i => i.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Navigation(o => o.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<OrderItem>(builder =>
        {
            builder.ToTable("order_items");
            builder.HasKey(i => i.Id);
            builder.Property(i => i.UserId).IsRequired();
            builder.Property(i => i.GameId).IsRequired();
            builder.Property(i => i.Price).HasColumnType("numeric(10,2)");
            builder.Property(i => i.Status).HasConversion<string>().HasMaxLength(20);
            builder.HasIndex(i => i.GameId);

            // Rule: "duplicate game ids are not allowed in the same order" —
            // enforced by the DB itself, not just request validation.
            builder.HasIndex(i => new { i.OrderId, i.GameId }).IsUnique();

            // Rule: "a user can't buy a game they already own or have
            // pending" — a partial unique index across ALL of a user's
            // orders (not just this one), excluding Failed items since a
            // failed order must never block a retry (instructions.md §10).
            // This is the race-proof backstop behind OrderService's
            // pre-flight GetConflictingGameIdsAsync check: two concurrent
            // requests for the same user+game can both pass that check,
            // but only one insert can win here — the other fails with a
            // Postgres unique-violation, which OrderService translates
            // into Error.Conflict. Requires OrderItem.Status to mirror
            // Order.Status (see OrderItem.SyncStatus) since a partial
            // index's predicate can only reference columns on its own
            // table.
            // Npgsql quotes mixed-case identifiers, so the raw filter must
            // match the physical column's exact case ("Status", not
            // "status") or Postgres won't resolve it.
            builder.HasIndex(i => new { i.UserId, i.GameId })
                .IsUnique()
                .HasFilter("\"Status\" <> 'Failed'");
        });

        modelBuilder.Entity<OrderEvent>(builder =>
        {
            builder.ToTable("order_events");
            builder.HasKey(e => e.Id);
            builder.Property(e => e.EventType).IsRequired().HasMaxLength(50);
            builder.Property(e => e.Payload).IsRequired();
            builder.HasIndex(e => e.OrderId);
        });

        // MassTransit's EF Core transactional outbox — see notes.md 15.
        // Kept in the same "orders" schema as the aggregate it protects.
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}
