using System.Diagnostics.CodeAnalysis;

namespace HubPedidos.Domain.Comum;

public sealed class Resultado<T>
{
    private Resultado(T? valor, IReadOnlyList<string> erros)
    {
        Valor = valor;
        Erros = erros;
    }

    public T? Valor { get; }

    public IReadOnlyList<string> Erros { get; }

    public bool Sucesso => Erros.Count == 0;

    public static Resultado<T> Ok(T valor) => new(valor, Array.Empty<string>());

    public static Resultado<T> Falha(IEnumerable<string> erros)
    {
        ArgumentNullException.ThrowIfNull(erros);
        var lista = erros.ToArray();
        if (lista.Length == 0)
        {
            throw new ArgumentException("Uma falha precisa de ao menos um erro.", nameof(erros));
        }

        return new Resultado<T>(default, Array.AsReadOnly(lista));
    }

    public static Resultado<T> Falha(string erro) => Falha(new[] { erro });

    public bool TryObter([NotNullWhen(true)] out T? valor)
    {
        valor = Valor;
        return Sucesso && valor is not null;
    }
}
