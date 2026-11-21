using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using relatorio007.Models;

namespace relatorio007.Services;

public static class Etag
{
    // a representacao e formada por Id, Status, Total e Versao (o instante nao entra)
    public static string Gerar(PedidoDto p)
    {
        var texto = $"{p.Id}|{p.Status}|{p.Total.ToString(CultureInfo.InvariantCulture)}|{p.Versao}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(texto));
        return "\"" + Convert.ToHexString(hash)[..16] + "\"";
    }

    public static bool Confere(string? ifNoneMatch, string etagAtual)
    {
        if (string.IsNullOrWhiteSpace(ifNoneMatch))
            return false;

        if (ifNoneMatch.Trim() == "*")
            return true;

        foreach (var parte in ifNoneMatch.Split(','))
        {
            var valor = parte.Trim();
            if (valor.StartsWith("W/"))
                valor = valor.Substring(2);

            if (valor == etagAtual)
                return true;
        }

        return false;
    }
}
