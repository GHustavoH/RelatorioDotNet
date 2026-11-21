using HubPedidos.Api.Infraestrutura;
using HubPedidos.Application;
using HubPedidos.Application.Contratos;
using HubPedidos.Application.Pedidos;
using HubPedidos.Application.Processamento;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

// PostgreSQL: connection string via ConnectionStrings__Postgres (variável de ambiente ou user-secrets).
// Sem ela, a API usa o repositório em memória (útil para desenvolvimento e demonstração).
var connectionString = builder.Configuration.GetConnectionString("Postgres");
if (!string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services.AddSingleton(NpgsqlDataSource.Create(connectionString));
    builder.Services.AddSingleton<IPedidoRepositorio, PedidoRepositorioPostgres>();
}
else
{
    builder.Services.AddSingleton<IPedidoRepositorio, RepositorioEmMemoria>();
}

WebApplication app;
try
{
    builder.Services.AddHubPedidosApplication();
    app = builder.Build();
    app.Services.GetRequiredService<CatalogoProcessadores>().ValidarResolucao(app.Services);
}
catch (CatalogoProcessadoresException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

await app.Services.GetRequiredService<IPedidoRepositorio>().InicializarAsync(app.Lifetime.ApplicationStopping);

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapPost("/api/pedidos", async (
    CriarPedidoRequest? request,
    ServicoPedidos servico,
    ILogger<ServicoPedidos> logger,
    CancellationToken cancellationToken) =>
{
    var resultado = await servico.ProcessarAsync(request, cancellationToken);

    if (resultado is ResultadoPedido.Invalido falha)
    {
        logger.LogWarning("Conversão do DTO falhou com {Quantidade} erro(s).", falha.Erros.Count);
    }

    IResult resposta = resultado switch
    {
        ResultadoPedido.Criado criado => Results.Json(criado.Pedido, statusCode: StatusCodes.Status201Created),
        ResultadoPedido.Invalido invalido => Results.ValidationProblem(AgruparErros(invalido.Erros)),
        ResultadoPedido.Conflito conflito => Results.Conflict(new { mensagem = conflito.Mensagem }),
        _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
    };

    return resposta;
});

app.MapGet("/api/processadores", (ServicoPedidos servico) => Results.Ok(servico.ListarProcessadores()));

await app.RunAsync();
return 0;

static Dictionary<string, string[]> AgruparErros(IReadOnlyList<string> erros) =>
    erros
        .GroupBy(e => e.Contains(':') ? e[..e.IndexOf(':')] : "pedido")
        .ToDictionary(
            g => g.Key,
            g => g.Select(e => e.Contains(':') ? e[(e.IndexOf(':') + 1)..].Trim() : e).ToArray());
