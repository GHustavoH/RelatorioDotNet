using System.Reflection;

namespace RelatorioFinal.Dominio;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class ProcessadorPedidoAttribute : Attribute
{
    public ProcessadorPedidoAttribute(CategoriaPedido categoria, int versao = 1)
    {
        Categoria = categoria;
        Versao = versao;
    }

    public CategoriaPedido Categoria { get; }
    public int Versao { get; }
}

public interface IProcessadorPedido
{
    string Processar(Pedido pedido);
}

[ProcessadorPedido(CategoriaPedido.Expresso)]
public sealed class ProcessadorExpresso : IProcessadorPedido
{
    public string Processar(Pedido pedido) =>
        $"Despacho expresso para {pedido.Cliente} com {pedido.Itens.Count} item(ns)";
}

[ProcessadorPedido(CategoriaPedido.Internacional)]
public sealed class ProcessadorInternacional : IProcessadorPedido
{
    public string Processar(Pedido pedido) =>
        $"Pedido internacional de {pedido.Cliente} enviado para analise aduaneira";
}

[ProcessadorPedido(CategoriaPedido.Corporativo)]
public sealed class ProcessadorCorporativo : IProcessadorPedido
{
    public string Processar(Pedido pedido) =>
        $"Pedido corporativo de {pedido.Cliente} no valor de {pedido.Total:F2} aguardando aprovacao";
}

[ProcessadorPedido(CategoriaPedido.Economico)]
public sealed class ProcessadorEconomico : IProcessadorPedido
{
    public string Processar(Pedido pedido) =>
        $"Pedido economico de {pedido.Cliente} agrupado no lote do dia";
}

[ProcessadorPedido(CategoriaPedido.Padrao)]
public sealed class ProcessadorPadrao : IProcessadorPedido
{
    public string Processar(Pedido pedido) =>
        $"Pedido padrao de {pedido.Cliente} processado normalmente";
}

public sealed class CatalogoProcessadores
{
    private readonly Dictionary<CategoriaPedido, IProcessadorPedido> _processadores = new();

    // varre somente o assembly informado e monta o catalogo uma vez
    public CatalogoProcessadores(Assembly assembly)
    {
        var tipos = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(IProcessadorPedido).IsAssignableFrom(t))
            .OrderBy(t => t.FullName, StringComparer.Ordinal);

        foreach (var tipo in tipos)
        {
            var atributo = tipo.GetCustomAttribute<ProcessadorPedidoAttribute>();
            if (atributo is null)
            {
                throw new InvalidOperationException($"O processador {tipo.Name} nao possui o atributo ProcessadorPedido");
            }

            if (tipo.GetConstructor(Type.EmptyTypes) is null)
            {
                throw new InvalidOperationException($"O processador {tipo.Name} precisa de construtor sem parametros");
            }

            var instancia = (IProcessadorPedido)Activator.CreateInstance(tipo)!;

            if (!_processadores.TryAdd(atributo.Categoria, instancia))
            {
                throw new InvalidOperationException($"Ja existe processador para a categoria {atributo.Categoria}");
            }
        }
    }

    public IProcessadorPedido Obter(CategoriaPedido categoria)
    {
        if (_processadores.TryGetValue(categoria, out var processador))
        {
            return processador;
        }

        throw new InvalidOperationException($"Nao existe processador para a categoria {categoria}");
    }
}
