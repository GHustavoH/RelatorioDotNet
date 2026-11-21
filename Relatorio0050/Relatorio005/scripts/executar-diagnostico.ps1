param(
    [string]$Experimento = "experimento.json",
    [string]$PastaBase = "resultados",
    [int]$TraceDuracaoS = 20,
    [switch]$SemCounters,
    [switch]$SemTrace,
    [switch]$ExecutarBenchmark
)

$ErrorActionPreference = "Stop"
$raiz = Split-Path -Parent $PSScriptRoot
Set-Location $raiz

$carimbo = Get-Date -Format "yyyyMMdd-HHmmss"
$pasta = Join-Path $PastaBase $carimbo
New-Item -ItemType Directory -Force -Path $pasta | Out-Null

dotnet --info | Out-File (Join-Path $pasta "dotnet-info.txt")
Copy-Item $Experimento (Join-Path $pasta "experimento.json")

dotnet build Relatorio005.slnx -c Release
if ($LASTEXITCODE -ne 0) { throw "Falha na compilacao Release." }

$apiDll = Join-Path $raiz "Relatorio005/bin/Release/net10.0/Relatorio005.dll"
$loadDll = Join-Path $raiz "Relatorio005.LoadTest/bin/Release/net10.0/Relatorio005.LoadTest.dll"

$variaveis = @{}
dotnet $loadDll variaveis $Experimento | ForEach-Object {
    $par = $_ -split "=", 2
    $variaveis[$par[0]] = $par[1]
}

$url = $variaveis["LT_URL"]
$aquecimento = [int]$variaveis["LT_AQUECIMENTO_S"]
$medicao = [int]$variaveis["LT_MEDICAO_S"]
$duracaoColeta = [TimeSpan]::FromSeconds($aquecimento + $medicao + 10).ToString("hh\:mm\:ss")
$duracaoTrace = [TimeSpan]::FromSeconds($TraceDuracaoS).ToString("hh\:mm\:ss")

function Invoke-Rodada {
    param([string]$Rotulo, [string]$Estrategia)

    foreach ($chave in $variaveis.Keys) {
        if ($chave -like "Hub__*") { Set-Item -Path "env:$chave" -Value $variaveis[$chave] }
    }

    $env:Hub__Estrategia = $Estrategia
    $env:ASPNETCORE_ENVIRONMENT = "Production"
    $env:ASPNETCORE_URLS = $url
    $env:Logging__LogLevel__Default = "Warning"

    $api = Start-Process dotnet -ArgumentList $apiDll -PassThru -NoNewWindow `
        -RedirectStandardOutput (Join-Path $pasta "$Rotulo-api.log") `
        -RedirectStandardError (Join-Path $pasta "$Rotulo-api.err.log")

    try {
        $pronto = $false
        for ($i = 0; $i -lt 60 -and -not $pronto; $i++) {
            try { Invoke-WebRequest "$url/health" -UseBasicParsing | Out-Null; $pronto = $true }
            catch { Start-Sleep -Milliseconds 500 }
        }
        if (-not $pronto) { throw "A API nao respondeu em $url." }

        $contadores = $null
        if (-not $SemCounters) {
            $arquivoContadores = Join-Path $pasta "$Rotulo-counters.csv"
            $contadores = Start-Process dotnet-counters -PassThru -NoNewWindow -ArgumentList @(
                "collect", "--process-id", $api.Id,
                "--counters", "System.Runtime,Microsoft.AspNetCore.Hosting,Relatorio005.Hub",
                "--format", "csv", "--output", $arquivoContadores,
                "--refresh-interval", "1", "--duration", $duracaoColeta)
        }

        $trace = $null
        if (-not $SemTrace) {
            $arquivoTrace = Join-Path $pasta "$Rotulo.nettrace"
            $trace = Start-Job -ScriptBlock {
                param($espera, $processo, $duracao, $saida)
                Start-Sleep -Seconds $espera
                & dotnet-trace collect --process-id $processo --profile "dotnet-common,dotnet-sampled-thread-time" --duration $duracao --output $saida
            } -ArgumentList ($aquecimento + 5), $api.Id, $duracaoTrace, $arquivoTrace
        }

        dotnet $loadDll --experimento $Experimento --rotulo $Rotulo --saida $pasta

        if ($contadores) { $contadores | Wait-Process -Timeout 60 -ErrorAction SilentlyContinue }
        if ($trace) {
            Wait-Job $trace -Timeout 120 | Out-Null
            Receive-Job $trace | Out-File (Join-Path $pasta "$Rotulo-trace.log")
            Remove-Job $trace -Force
            if (Test-Path $arquivoTrace) {
                dotnet-trace convert $arquivoTrace --format Speedscope
            }
        }
    }
    finally {
        if (-not $api.HasExited) { Stop-Process -Id $api.Id -Force }
    }
}

Invoke-Rodada -Rotulo "antes" -Estrategia "Baseline"
Invoke-Rodada -Rotulo "depois" -Estrategia "Otimizada"

dotnet test Relatorio005.Tests -c Release --logger "trx;LogFileName=testes.trx" --results-directory $pasta
$codigoTestes = $LASTEXITCODE

if ($ExecutarBenchmark) {
    dotnet run -c Release --project Relatorio005.Benchmarks -- --filter "*" --artifacts (Join-Path $pasta "benchmarkdotnet")
}

dotnet $loadDll comparar (Join-Path $pasta "antes.json") (Join-Path $pasta "depois.json") | Tee-Object (Join-Path $pasta "comparacao.txt")

Write-Host "Evidencias em: $pasta"
if ($codigoTestes -ne 0) { Write-Host "ATENCAO: testes funcionais falharam."; exit $codigoTestes }
