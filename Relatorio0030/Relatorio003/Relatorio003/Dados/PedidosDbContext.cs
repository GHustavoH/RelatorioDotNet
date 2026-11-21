using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Relatorio003.Dominio;

namespace Relatorio003.Dados;

public class PedidosDbContext(DbContextOptions<PedidosDbContext> options) : DbContext(options)
{
    public DbSet<Pedido> Pedidos => Set<Pedido>();
    public DbSet<ItemPedido> Itens => Set<ItemPedido>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PedidosDbContext).Assembly);
    }
}

public class PedidoConfiguration : IEntityTypeConfiguration<Pedido>
{
    public void Configure(EntityTypeBuilder<Pedido> b)
    {
        b.ToTable("Pedidos");
        b.HasKey(p => p.Id);

        b.Property(p => p.Numero).HasMaxLength(20).IsRequired();
        b.Property(p => p.Cliente).HasMaxLength(120).IsRequired();
        b.Property(p => p.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(p => p.CriadoEm).IsRequired();
        b.Property(p => p.Total).HasPrecision(18, 2);

        // Índices alinhados às consultas do painel (ordenação determinística: CriadoEm desc, Id desc).
        b.HasIndex(p => p.Numero).IsUnique().HasDatabaseName("IX_Pedidos_Numero");
        b.HasIndex(p => new { p.CriadoEm, p.Id }).IsDescending(true, true)
            .HasDatabaseName("IX_Pedidos_CriadoEm_Id");
        b.HasIndex(p => new { p.Status, p.CriadoEm, p.Id }).IsDescending(false, true, true)
            .HasDatabaseName("IX_Pedidos_Status_CriadoEm_Id");

        // Alternativa relacional: colunas EnderecoEntrega_Logradouro, EnderecoEntrega_Cidade, ...
        b.ComplexProperty(p => p.EnderecoEntrega, e =>
        {
            e.Property(x => x.Logradouro).HasMaxLength(150).IsRequired();
            e.Property(x => x.Cidade).HasMaxLength(80).IsRequired();
            e.Property(x => x.Uf).HasMaxLength(2).IsRequired();
            e.Property(x => x.Cep).HasMaxLength(8).IsRequired();
        });

        // Alternativa JSON: uma única coluna jsonb "Metadados".
        b.ComplexProperty(p => p.Metadados, m => m.ToJson());

        b.HasMany(p => p.Itens)
            .WithOne(i => i.Pedido)
            .HasForeignKey(i => i.PedidoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ItemPedidoConfiguration : IEntityTypeConfiguration<ItemPedido>
{
    public void Configure(EntityTypeBuilder<ItemPedido> b)
    {
        b.ToTable("ItensPedido");
        b.HasKey(i => i.Id);
        b.Property(i => i.Produto).HasMaxLength(120).IsRequired();
        b.Property(i => i.PrecoUnitario).HasPrecision(18, 2);
        b.HasIndex(i => i.PedidoId).HasDatabaseName("IX_ItensPedido_PedidoId");
    }
}
