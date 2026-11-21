using Microsoft.EntityFrameworkCore;
using Relatorio003.Dominio;

namespace Relatorio003.Dados;

/// <summary>Gera massa de dados determinística (mesma semente => mesmos dados). Idempotente.</summary>
public static class GeradorDeMassa
{
    private static readonly (string Cidade, string Uf)[] Cidades =
    [
        ("São Paulo", "SP"), ("Campinas", "SP"), ("Artur Nogueira", "SP"),
        ("Rio de Janeiro", "RJ"), ("Belo Horizonte", "MG"), ("Curitiba", "PR")
    ];

    private static readonly string[] Canais = ["app", "site", "loja", "telefone"];
    private static readonly string[] Tags = ["promo", "presente", "urgente", "corporativo", "recorrente"];
    private static readonly string[] Produtos = ["Teclado", "Mouse", "Monitor", "Cabo HDMI", "Headset", "Webcam", "SSD", "Hub USB"];

    public static async Task GerarAsync(PedidosDbContext db, int quantidade, int semente = 42, CancellationToken ct = default)
    {
        if (await db.Pedidos.AnyAsync(ct)) return;

        var rnd = new Random(semente);
        var inicio = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        const int lote = 1000;

        for (var baseIdx = 0; baseIdx < quantidade; baseIdx += lote)
        {
            var fim = Math.Min(baseIdx + lote, quantidade);
            for (var i = baseIdx; i < fim; i++)
            {
                var (cidade, uf) = Cidades[rnd.Next(Cidades.Length)];
                var pedido = new Pedido
                {
                    Numero = $"P{i + 1:D8}",
                    Cliente = $"Cliente {rnd.Next(1, 400):D3}",
                    Status = (StatusPedido)rnd.Next(0, 5),
                    // i / 3 três pedidos por timestamp, para exercitar o desempate por Id.
                    CriadoEm = inicio.AddMinutes((i / 3) * 7),
                    EnderecoEntrega = new Endereco
                    {
                        Logradouro = $"Rua {rnd.Next(1, 500)}, {rnd.Next(1, 999)}",
                        Cidade = cidade,
                        Uf = uf,
                        Cep = rnd.Next(10_000_000, 99_999_999).ToString()
                    },
                    Metadados = new MetadadosPedido
                    {
                        Canal = Canais[rnd.Next(Canais.Length)],
                        Cupom = rnd.Next(4) == 0 ? "PROMO10" : null,
                        Tags = Tags.Where(_ => rnd.Next(4) == 0).ToList()
                    }
                };

                var qtdItens = rnd.Next(1, 6);
                for (var k = 0; k < qtdItens; k++)
                {
                    pedido.Itens.Add(new ItemPedido
                    {
                        Produto = Produtos[rnd.Next(Produtos.Length)],
                        Quantidade = rnd.Next(1, 5),
                        PrecoUnitario = rnd.Next(1000, 90000) / 100m
                    });
                }
                pedido.Total = pedido.Itens.Sum(x => x.Quantidade * x.PrecoUnitario);
                db.Pedidos.Add(pedido);
            }

            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();
        }
    }
}
