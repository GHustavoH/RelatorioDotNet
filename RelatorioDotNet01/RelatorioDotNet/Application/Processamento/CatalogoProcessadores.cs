using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using HubPedidos.Domain.Classificacao;

namespace HubPedidos.Application.Processamento;

public sealed class CatalogoProcessadores
{
    private readonly ProcessadorDescritor[] _descritores;
    private readonly Dictionary<CategoriaProcessamento, ProcessadorDescritor> _vigentes;

    private CatalogoProcessadores(
        ProcessadorDescritor[] descritores,
        Dictionary<CategoriaProcessamento, ProcessadorDescritor> vigentes)
    {
        _descritores = descritores;
        _vigentes = vigentes;
    }

    public IReadOnlyList<ProcessadorDescritor> Descritores => _descritores;

    public bool TryObter(CategoriaProcessamento categoria, [NotNullWhen(true)] out ProcessadorDescritor? descritor) =>
        _vigentes.TryGetValue(categoria, out descritor);

    public bool EhVigente(ProcessadorDescritor descritor) =>
        _vigentes.TryGetValue(descritor.Categoria, out var vigente) && vigente == descritor;

    public static CatalogoProcessadores Construir(Assembly assembly, bool exigirCobertura = false)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        return Construir(ObterTipos(assembly), exigirCobertura);
    }

    public static CatalogoProcessadores Construir(IEnumerable<Type> tipos, bool exigirCobertura = false)
    {
        ArgumentNullException.ThrowIfNull(tipos);

        var problemas = new List<string>();
        var validos = new List<ProcessadorDescritor>();

        var candidatos = tipos.Where(t => t.IsClass && !t.IsAbstract && !t.IsGenericTypeDefinition);

        foreach (var tipo in candidatos)
        {
            var atributo = tipo.GetCustomAttribute<ProcessadorPedidoAttribute>(inherit: false);
            var implementaInterface = typeof(IProcessadorPedido).IsAssignableFrom(tipo);

            if (atributo is null && !implementaInterface)
            {
                continue; // não é processador: ignorado
            }

            var nome = tipo.FullName ?? tipo.Name;

            if (atributo is null)
            {
                problemas.Add($"{nome}: implementa IProcessadorPedido mas não declara [ProcessadorPedido].");
                continue;
            }

            var valido = true;

            if (!implementaInterface)
            {
                problemas.Add($"{nome}: declara [ProcessadorPedido] mas não implementa IProcessadorPedido.");
                valido = false;
            }

            if (atributo.Categoria == CategoriaProcessamento.Invalido || !Enum.IsDefined(atributo.Categoria))
            {
                problemas.Add($"{nome}: categoria '{atributo.Categoria}' não pode ser associada a um processador.");
                valido = false;
            }

            if (atributo.Versao <= 0)
            {
                problemas.Add($"{nome}: versão deve ser maior que zero (valor: {atributo.Versao}).");
                valido = false;
            }

            var construtores = tipo.GetConstructors();
            if (construtores.Length != 1)
            {
                problemas.Add($"{nome}: esperado exatamente 1 construtor público (encontrados: {construtores.Length}).");
                valido = false;
            }

            if (valido)
            {
                validos.Add(new ProcessadorDescritor(atributo.Categoria, atributo.Versao, tipo));
            }
        }

        foreach (var grupo in validos.GroupBy(d => (d.Categoria, d.Versao)).Where(g => g.Count() > 1))
        {
            var nomes = string.Join(", ", grupo.Select(d => d.Tipo.FullName).OrderBy(n => n, StringComparer.Ordinal));
            problemas.Add($"Duplicidade para categoria {grupo.Key.Categoria} versão {grupo.Key.Versao}: {nomes}.");
        }

        if (exigirCobertura)
        {
            foreach (var categoria in Enum.GetValues<CategoriaProcessamento>().Where(c => c != CategoriaProcessamento.Invalido))
            {
                if (validos.All(d => d.Categoria != categoria))
                {
                    problemas.Add($"Categoria {categoria} não possui nenhum processador.");
                }
            }
        }

        if (problemas.Count > 0)
        {
            throw new CatalogoProcessadoresException(problemas);
        }

        var ordenados = validos
            .OrderBy(d => d.Categoria)
            .ThenBy(d => d.Versao)
            .ThenBy(d => d.Tipo.FullName, StringComparer.Ordinal)
            .ToArray();

        var vigentes = ordenados
            .GroupBy(d => d.Categoria)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(d => d.Versao).First());

        return new CatalogoProcessadores(ordenados, vigentes);
    }

    public void ValidarResolucao(IServiceProvider servicos)
    {
        ArgumentNullException.ThrowIfNull(servicos);

        var problemas = new List<string>();

        foreach (var descritor in _descritores)
        {
            try
            {
                if (servicos.GetService(descritor.Tipo) is not IProcessadorPedido)
                {
                    problemas.Add($"{descritor.Tipo.FullName}: não está registrado no contêiner de serviços.");
                }
            }
            catch (Exception ex)
            {
                problemas.Add($"{descritor.Tipo.FullName}: falha ao instanciar ({ex.Message}).");
            }
        }

        if (problemas.Count > 0)
        {
            throw new CatalogoProcessadoresException(problemas);
        }
    }

    private static Type[] ObterTipos(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            var problemas = ex.LoaderExceptions
                .Where(e => e is not null)
                .Select(e => $"{assembly.GetName().Name}: {e!.Message}")
                .ToArray();

            throw new CatalogoProcessadoresException(
                problemas.Length > 0 ? problemas : new[] { $"{assembly.GetName().Name}: falha ao carregar tipos." });
        }
    }
}
