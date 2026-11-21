using System.Data.Common;
using System.Text;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Relatorio003.Diagnostico;

public sealed record ComandoSqlCapturado(string Sql, TimeSpan Duracao);

public sealed class LimiteDeComandosExcedidoException(int limite, IReadOnlyList<ComandoSqlCapturado> comandos)
    : Exception(Montar(limite, comandos))
{
    private static string Montar(int limite, IReadOnlyList<ComandoSqlCapturado> comandos)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Limite de comandos SQL excedido: {comandos.Count} executados, máximo permitido {limite}.");
        for (var i = 0; i < comandos.Count; i++)
            sb.AppendLine($"  [{i + 1}] {comandos[i].Sql.ReplaceLineEndings(" ")}");
        return sb.ToString();
    }
}


// Interceptor que registra cada comando SQL executado (texto + duração; nunca valores de parâmetros).
//Usado nos testes (orçamento de comandos / detecção de N+1) e na medição (tempo de banco).
// A duração é a do envio/execução do comando no provedor, aproximação do "tempo de banco".

public sealed class ContadorComandosSql : DbCommandInterceptor
{
    private readonly object _trava = new();
    private readonly List<ComandoSqlCapturado> _comandos = [];

    public int Total { get { lock (_trava) return _comandos.Count; } }

    public IReadOnlyList<ComandoSqlCapturado> Comandos { get { lock (_trava) return [.. _comandos]; } }

    public TimeSpan DuracaoTotal { get { lock (_trava) return TimeSpan.FromTicks(_comandos.Sum(c => c.Duracao.Ticks)); } }

    public void Reiniciar() { lock (_trava) _comandos.Clear(); }

    //Falha (exceção) quando mais comandos que o limite foram executados.
    public void GarantirNoMaximo(int limite)
    {
        var copia = Comandos;
        if (copia.Count > limite) throw new LimiteDeComandosExcedidoException(limite, copia);
    }

    private void Registrar(DbCommand comando, CommandExecutedEventData dados)
    {
        lock (_trava) _comandos.Add(new ComandoSqlCapturado(comando.CommandText, dados.Duration));
    }

    public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
    { Registrar(command, eventData); return result; }

    public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
    { Registrar(command, eventData); return new(result); }

    public override int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result)
    { Registrar(command, eventData); return result; }

    public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default)
    { Registrar(command, eventData); return new(result); }

    public override object? ScalarExecuted(DbCommand command, CommandExecutedEventData eventData, object? result)
    { Registrar(command, eventData); return result; }

    public override ValueTask<object?> ScalarExecutedAsync(DbCommand command, CommandExecutedEventData eventData, object? result, CancellationToken cancellationToken = default)
    { Registrar(command, eventData); return new(result); }
}
