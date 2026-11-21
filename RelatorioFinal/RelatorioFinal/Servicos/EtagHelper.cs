using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Primitives;

namespace RelatorioFinal.Servicos;

public static class EtagHelper
{
    // o etag vem do hash do JSON da representacao entao muda quando qualquer campo muda
    public static string Gerar(object representacao)
    {
        var json = JsonSerializer.Serialize(representacao, representacao.GetType());
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return "\"" + Convert.ToHexString(hash)[..16] + "\"";
    }

    public static bool Confere(StringValues cabecalho, string etag)
    {
        foreach (var valor in cabecalho)
        {
            if (valor is null)
            {
                continue;
            }

            var partes = valor.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            foreach (var parte in partes)
            {
                if (parte == "*" || parte.Replace("W/", "") == etag)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
