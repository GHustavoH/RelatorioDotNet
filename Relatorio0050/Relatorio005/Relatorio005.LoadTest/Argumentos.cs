using System.Globalization;

namespace Relatorio005.LoadTest;

public sealed class Argumentos
{
    private readonly Dictionary<string, string> _valores = new(StringComparer.OrdinalIgnoreCase);

    private Argumentos()
    {
    }

    public static Argumentos Ler(IEnumerable<string> args)
    {
        var resultado = new Argumentos();
        var lista = args.ToArray();

        for (var i = 0; i < lista.Length; i++)
        {
            if (!lista[i].StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            var chave = lista[i][2..];
            var valor = i + 1 < lista.Length && !lista[i + 1].StartsWith("--", StringComparison.Ordinal)
                ? lista[++i]
                : "true";

            resultado._valores[chave] = valor;
        }

        return resultado;
    }

    public string? Texto(string chave) => _valores.TryGetValue(chave, out var valor) ? valor : null;

    public string Texto(string chave, string padrao) => Texto(chave) ?? padrao;

    public double Real(string chave, double padrao) =>
        Texto(chave) is { } valor ? double.Parse(valor, CultureInfo.InvariantCulture) : padrao;

    public int Inteiro(string chave, int padrao) =>
        Texto(chave) is { } valor ? int.Parse(valor, CultureInfo.InvariantCulture) : padrao;
}
