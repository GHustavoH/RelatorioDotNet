using Microsoft.EntityFrameworkCore;

namespace RelatorioFinal.Persistencia;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<PedidoEntidade> Pedidos => Set<PedidoEntidade>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PedidoEntidade>(e =>
        {
            e.ToTable("pedidos");
            e.HasKey(p => p.Id);
            e.Property(p => p.Id).ValueGeneratedNever();
            e.Property(p => p.Cliente).HasMaxLength(100).IsRequired();
            e.Property(p => p.Regiao).HasMaxLength(50).IsRequired();
            e.Property(p => p.Prioridade).HasMaxLength(20).IsRequired();
            e.Property(p => p.Categoria).HasMaxLength(20).IsRequired();
            e.Property(p => p.Status).HasMaxLength(20).IsRequired();
            e.Property(p => p.Total).HasPrecision(18, 2);

            // indices pensados para os filtros da listagem
            e.HasIndex(p => new { p.Status, p.CriadoEm });
            e.HasIndex(p => p.Cliente);

            e.ComplexProperty(p => p.Endereco);
            e.ComplexProperty(p => p.Metadados, c => c.ToJson());

            e.HasMany(p => p.Itens)
                .WithOne(i => i.Pedido)
                .HasForeignKey(i => i.PedidoId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ItemPedidoEntidade>(e =>
        {
            e.ToTable("itens_pedido");
            e.Property(i => i.Produto).HasMaxLength(100).IsRequired();
            e.Property(i => i.Preco).HasPrecision(18, 2);
        });
    }
}
