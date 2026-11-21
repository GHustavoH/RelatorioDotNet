using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace relatorio007.Auth;

// autenticacao simplificada para o trabalho: usuario vem no header X-Usuario
public class HeaderAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string Esquema = "Header";

    public HeaderAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var usuario = Request.Headers["X-Usuario"].ToString();
        if (string.IsNullOrEmpty(usuario))
            return Task.FromResult(AuthenticateResult.NoResult());

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, usuario),
            new Claim(ClaimTypes.Name, usuario)
        };

        if (Request.Headers["X-Perfil"].ToString() == "admin")
            claims.Add(new Claim(ClaimTypes.Role, "admin"));

        var identidade = new ClaimsIdentity(claims, Esquema);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identidade), Esquema);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
