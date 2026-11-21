using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

public class ApiKeyHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IConfiguration _config;

    public ApiKeyHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IConfiguration config) : base(options, logger, encoder)
    {
        _config = config;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Api-Key", out var chave))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        // a chave vem da configuracao, nunca fica no codigo
        var esperada = _config["ApiKey"];
        if (string.IsNullOrEmpty(esperada) || chave != esperada)
        {
            return Task.FromResult(AuthenticateResult.Fail("Chave invalida"));
        }

        var identidade = new ClaimsIdentity(new Claim[] { new(ClaimTypes.Name, "cliente-api") }, Scheme.Name);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identidade), Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
