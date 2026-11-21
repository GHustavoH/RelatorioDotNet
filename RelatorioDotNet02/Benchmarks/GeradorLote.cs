using System.Text;

namespace HubPedidos.Benchmarks;

// Dados sintéticos e determinísticos (Random com semente fixa) -> benchmark reproduzível.
// Sem dado real: nenhum segredo, credencial ou informação sensível.
public static class GeradorLote
{
    public const int Semente = 42;

    public static string Gerar(int quantidadePedidos)
    {
        var rnd = new Random(Semente);
        var regioes = new[] { "Sudeste", "Sul", "Nordeste", "CentroOeste", "Norte", "Exterior" };
        var prioridades = new[] { "Baixa", "Normal", "Alta", "Urgente" };
        var sb = new StringBuilder();

        for (var i = 1; i <= quantidadePedidos; i++)
        {
            var id = new Guid(i, 0, 0, new byte[8]);
            var regiao = i % 20 == 0 ? "Marte" : regioes[rnd.Next(regioes.Length)]; // 5% de linhas inválidas
            sb.Append(id).Append(';').Append(regiao).Append(';').Append(prioridades[rnd.Next(prioridades.Length)]).Append(';');

            var itens = rnd.Next(1, 6);
            for (var j = 0; j < itens; j++)
            {
                if (j > 0) sb.Append('|');
                sb.Append("SKU").Append(j).Append('-').Append(rnd.Next(1000, 9999))
                  .Append(':').Append(rnd.Next(1, 20))
                  .Append(':').Append(rnd.Next(1, 500)).Append('.').Append(rnd.Next(0, 100).ToString("00"));
            }

            if (i < quantidadePedidos) sb.Append('\n');
        }

        return sb.ToString();
    }
}
