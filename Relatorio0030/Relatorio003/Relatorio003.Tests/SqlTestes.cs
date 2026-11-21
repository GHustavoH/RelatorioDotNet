using System.Text.RegularExpressions;

namespace Relatorio003.Tests;

public static class SqlTestes
{
    /// <summary>Troca nomes de parâmetros (@p, @__status_0, @status...) por @p para comparar a forma do SQL.</summary>
    public static string Normalizar(string sql) => Regex.Replace(sql, @"@\w+", "@p");
}

/// <summary>JSON complexo exige EF Core 10+; o teste é ignorado fora dessa versão (registrada no LEIAME).</summary>
public sealed class JsonSuportadoFactAttribute : FactAttribute
{
    public JsonSuportadoFactAttribute()
    {
        var ef = typeof(Microsoft.EntityFrameworkCore.DbContext).Assembly.GetName().Version;
        if (ef is null || ef.Major < 10)
            Skip = "Mapeamento complexo/JSON validado somente com EF Core 10+ e Npgsql 10+.";
    }
}
