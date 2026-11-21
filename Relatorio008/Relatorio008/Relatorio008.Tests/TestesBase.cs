namespace Relatorio008.Tests;

// cada teste cria sua propria fabrica, entao cada um tem seu proprio banco
public abstract class TestesBase : IDisposable
{
    protected FabricaApi Fabrica { get; }
    protected HttpClient Cliente { get; }

    protected TestesBase()
    {
        Fabrica = new FabricaApi();
        Fabrica.PrepararBanco();
        Cliente = Fabrica.CriarCliente();
    }

    public void Dispose()
    {
        Cliente.Dispose();
        Fabrica.Dispose();
        GC.SuppressFinalize(this);
    }
}
