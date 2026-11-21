using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Relatorio005.LoadTest;

public static class Ambiente
{
    public static AmbienteInfo Coletar() => new(
        RuntimeInformation.OSDescription,
        RuntimeInformation.OSArchitecture.ToString(),
        NomeProcessador(),
        Environment.ProcessorCount,
        GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024 * 1024),
        RuntimeInformation.FrameworkDescription,
        VersaoSdk(),
        Compilacao(),
        DateTime.UtcNow);

    private static string Compilacao()
    {
#if DEBUG
        return "Debug";
#else
        return "Release";
#endif
    }

    private static string NomeProcessador()
    {
        try
        {
            var windows = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER");
            if (!string.IsNullOrWhiteSpace(windows))
            {
                return windows;
            }

            if (File.Exists("/proc/cpuinfo"))
            {
                var linha = File.ReadLines("/proc/cpuinfo")
                    .FirstOrDefault(l => l.StartsWith("model name", StringComparison.OrdinalIgnoreCase));

                if (linha is not null)
                {
                    return linha[(linha.IndexOf(':') + 1)..].Trim();
                }
            }
        }
        catch (IOException)
        {
        }

        return "desconhecido";
    }

    private static string VersaoSdk()
    {
        try
        {
            var informacao = new ProcessStartInfo("dotnet", "--version")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false
            };

            using var processo = Process.Start(informacao);
            if (processo is null)
            {
                return "desconhecido";
            }

            var saida = processo.StandardOutput.ReadToEnd().Trim();
            processo.WaitForExit(5000);
            return string.IsNullOrEmpty(saida) ? "desconhecido" : saida;
        }
        catch (Exception)
        {
            return "desconhecido";
        }
    }
}
