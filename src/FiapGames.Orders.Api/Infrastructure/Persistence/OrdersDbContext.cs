using FiapGames.Orders.Api.Domain;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace FiapGames.Orders.Api.Infrastructure.Persistence;

public sealed class OrdersDbContext : DbContext
{
    public const string Schema = "orders";

    public DbSet<Order> Orders => Set<Order>();

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
            builder.Property(o => o.GameId).IsRequired();
            builder.Property(o => o.Price).HasColumnType("numeric(10,2)");
            builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
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
