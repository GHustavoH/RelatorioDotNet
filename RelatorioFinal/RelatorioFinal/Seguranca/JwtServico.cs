using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace RelatorioFinal.Seguranca;

public class JwtServico(IConfiguration config)
{
    public const string Emissor = "RelatorioFinal";

    // usuarios fixos apenas para fins didaticos
    private static readonly Dictionary<string, (string Senha, string Papel)> Usuarios = new()
    {
        ["ana"] = ("123", "cliente"),
        ["bruno"] = ("123", "cliente"),
        ["admin"] = ("admin", "admin")
    };

    public string? ValidarCredenciais(string usuario, string senha)
    {
        if (Usuarios.TryGetValue(usuario ?? "", out var dados) && dados.Senha == senha)
        {
            return dados.Papel;
        }

        return null;
    }

    public string GerarToken(string usuario, string papel)
    {
        var chave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Chave"]!));
        var descritor = new SecurityTokenDescriptor
        {
            Issuer = Emissor,
            Audience = Emissor,
            Expires = DateTime.UtcNow.AddHours(1),
            Claims = new Dictionary<string, object> { ["name"] = usuario, ["role"] = papel },
            SigningCredentials = new SigningCredentials(chave, SecurityAlgorithms.HmacSha256)
        };

        return new JsonWebTokenHandler().CreateToken(descritor);
    }
}
