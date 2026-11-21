using Microsoft.AspNetCore.Mvc;
using RelatorioFinal.Contratos;
using RelatorioFinal.Seguranca;

namespace RelatorioFinal.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(JwtServico jwt) : ControllerBase
{
    [HttpPost("login")]
    public ActionResult<LoginResponse> Login(LoginRequest req)
    {
        var papel = jwt.ValidarCredenciais(req.Usuario, req.Senha);
        if (papel is null)
        {
            return Unauthorized();
        }

        return new LoginResponse(jwt.GerarToken(req.Usuario, papel));
    }
}
