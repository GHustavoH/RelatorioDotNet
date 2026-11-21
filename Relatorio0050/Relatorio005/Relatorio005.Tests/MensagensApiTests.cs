using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Relatorio005.Hub;
using Relatorio005.Models;

namespace Relatorio005.Tests;

public sealed class HubFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseSetting("Hub:SeedQuantidade", "2000");
        builder.UseSetting("Hub:LatenciaPersistenciaMs", "0");
        builder.UseSetting("Hub:IteracoesHash", "1");
        builder.UseSetting("Hub:Workers", "2");
        builder.UseSetting("Hub:CapacidadeFila", "1000");
    }
}

public class MensagensApiTests : IClassFixture<HubFactory>
{
    private readonly HttpClient _cliente;

    public MensagensApiTests(HubFactory fabrica)
    {
        _cliente = fabrica.CreateClient();
    }

    [Fact]
    public async Task Health_responde_200()
    {
        var resposta = await _cliente.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
    }

    [Fact]
    public async Task Criar_mensagem_valida_retorna_202_e_fica_consultavel()
    {
        var resposta = await _cliente.PostAsJsonAsync("/api/mensagens", new { canal = "suporte", conteudo = "pedido de teste", prioridade = 3 });

        Assert.Equal(HttpStatusCode.Accepted, resposta.StatusCode);
        var corpo = await resposta.Content.ReadFromJsonAsync<RespostaCriacao>();
        Assert.NotNull(corpo);

        var mensagem = await AguardarMensagemAsync(corpo.Id);

        Assert.Equal("suporte", mensagem.Canal);
        Assert.Equal("pedido de teste", mensagem.Conteudo);
        Assert.Equal(3, mensagem.Prioridade);
    }

    [Theory]
    [InlineData("", "conteudo", 1)]
    [InlineData("suporte", "", 1)]
    [InlineData("suporte", "conteudo", 10)]
    [InlineData("suporte", "conteudo", -1)]
    public async Task Criar_mensagem_invalida_retorna_400(string canal, string conteudo, int prioridade)
    {
        var resposta = await _cliente.PostAsJsonAsync("/api/mensagens", new { canal, conteudo, prioridade });

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Fact]
    public async Task Obter_mensagem_inexistente_retorna_404()
    {
        var resposta = await _cliente.GetAsync($"/api/mensagens/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
    }

    [Fact]
    public async Task Busca_sem_termo_retorna_400()
    {
        var resposta = await _cliente.GetAsync("/api/mensagens/busca?canal=suporte");

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Fact]
    public async Task Busca_com_limite_fora_da_faixa_retorna_400()
    {
        var resposta = await _cliente.GetAsync("/api/mensagens/busca?canal=suporte&termo=pedido&limite=1000");

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Fact]
    public async Task Busca_na_massa_inicial_respeita_filtro_e_limite()
    {
        var resultado = await _cliente.GetFromJsonAsync<List<Mensagem>>("/api/mensagens/busca?canal=suporte&termo=pedido&limite=10");

        Assert.NotNull(resultado);
        Assert.NotEmpty(resultado);
        Assert.True(resultado.Count <= 10);
        Assert.All(resultado, m => Assert.Equal("suporte", m.Canal));
    }

    [Fact]
    public async Task Mensagem_criada_aparece_na_busca()
    {
        var marcador = $"marcador{Guid.NewGuid():N}";
        var resposta = await _cliente.PostAsJsonAsync("/api/mensagens", new { canal = "testes", conteudo = $"texto {marcador}", prioridade = 9 });
        var corpo = await resposta.Content.ReadFromJsonAsync<RespostaCriacao>();
        Assert.NotNull(corpo);

        await AguardarMensagemAsync(corpo.Id);

        var resultado = await _cliente.GetFromJsonAsync<List<Mensagem>>($"/api/mensagens/busca?canal=testes&termo={marcador}&limite=5");

        Assert.NotNull(resultado);
        Assert.Single(resultado);
        Assert.Equal(corpo.Id, resultado[0].Id);
    }

    [Fact]
    public async Task Estado_expoe_configuracao_do_hub()
    {
        var resposta = await _cliente.GetAsync("/api/diagnostico/estado");
        var texto = await resposta.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Contains("estrategia", texto);
        Assert.Contains("profundidadeFila", texto);
    }

    private async Task<Mensagem> AguardarMensagemAsync(Guid id)
    {
        var limite = DateTime.UtcNow.AddSeconds(10);

        while (DateTime.UtcNow < limite)
        {
            var resposta = await _cliente.GetAsync($"/api/mensagens/{id}");
            if (resposta.StatusCode == HttpStatusCode.OK)
            {
                var mensagem = await resposta.Content.ReadFromJsonAsync<Mensagem>();
                Assert.NotNull(mensagem);
                return mensagem;
            }

            await Task.Delay(25);
        }

        throw new TimeoutException($"A mensagem {id} nao foi processada no prazo.");
    }
}
