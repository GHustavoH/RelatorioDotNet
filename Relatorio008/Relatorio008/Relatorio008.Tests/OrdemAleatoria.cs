using Xunit.Abstractions;
using Xunit.Sdk;

[assembly: TestCaseOrderer("Relatorio008.Tests.OrdemAleatoria", "Relatorio008.Tests")]

namespace Relatorio008.Tests;

// embaralha a ordem dos testes. A semente pode ser fixada com a
// variavel SEMENTE_TESTES para reproduzir uma execucao que falhou.
public class OrdemAleatoria : ITestCaseOrderer
{
    public IEnumerable<TTestCase> OrderTestCases<TTestCase>(IEnumerable<TTestCase> testCases)
        where TTestCase : ITestCase
    {
        var texto = Environment.GetEnvironmentVariable("SEMENTE_TESTES");
        var semente = int.TryParse(texto, out var valor) ? valor : Environment.TickCount;
        var sorteio = new Random(semente);

        return testCases.OrderBy(_ => sorteio.Next()).ToList();
    }
}
