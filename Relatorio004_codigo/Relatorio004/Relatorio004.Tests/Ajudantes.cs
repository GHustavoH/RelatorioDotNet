using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Relatorio004.Models;
using Relatorio004.Services;

namespace Relatorio004.Tests;

// banco falso: mede quantos acessos acontecem ao mesmo tempo
public class FakeRepositorio : IPedidoRepositorio
{
    private readonly object _trava = new();
    private int _emUso = 0;
    private int _iniciados = 0;

    public int MaxEmUso { get; private set; }
    public int Iniciados => _iniciados;
    public int DelayMs { get; set; } = 50;
    public ConcurrentBag<Guid> Salvos { get; } = new();

    public async Task SalvarAsync(Guid id, string cliente, decimal valor, CancellationToken ct)
    {
        Interlocked.Increment(ref _iniciados);

        lock (_trava)
        {
            _emUso++;
            if (_emUso > MaxEmUso) MaxEmUso = _emUso;
        }

        try
        {
            await Task.Delay(DelayMs, ct);

            if (cliente == "erro")
            {
                throw new InvalidOperationException("erro de teste");
            }

            Salvos.Add(id);
        }
        finally
        {
            lock (_trava)
            {
                _emUso--;
            }
        }
    }
}

public static class Ajudantes
{
    public static ProcessarPedidoJob NovoJob(string cliente = "cliente")
    {
        return new ProcessarPedidoJob(Guid.NewGuid(), cliente, 10, DateTime.UtcNow);
    }

    public static IConfiguration Config(int capacidade = 10, int consumidores = 2, int limiteBanco = 2, bool drenar = false)
    {
        return new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Fila:Capacidade"] = capacidade.ToString(),
            ["Fila:Consumidores"] = consumidores.ToString(),
            ["Fila:LimiteBanco"] = limiteBanco.ToString(),
            ["Fila:DrenarNoShutdown"] = drenar.ToString()
        }).Build();
    }

    public static PedidoWorker CriarWorker(Channel<ProcessarPedidoJob> canal, IPedidoRepositorio repo,
        Metricas metricas, int consumidores, int limiteBanco, bool drenar)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IPedidoRepositorio>(repo);
        var provider = services.BuildServiceProvider();

        return new PedidoWorker(canal, provider.GetRequiredService<IServiceScopeFactory>(), metricas,
            NullLogger<PedidoWorker>.Instance, Config(10, consumidores, limiteBanco, drenar));
    }

    public static async Task<bool> Esperar(Func<bool> condicao, int timeoutMs = 5000)
    {
        var inicio = DateTime.UtcNow;
        while ((DateTime.UtcNow - inicio).TotalMilliseconds < timeoutMs)
        {
            if (condicao()) return true;
            await Task.Delay(10);
        }
        return condicao();
    }

    public static long Valor(this Metricas m, string nome)
    {
        return m.Todas().GetValueOrDefault(nome);
    }
}
