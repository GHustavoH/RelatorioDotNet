# Revisao automatizada local (mesmos passos do workflow do GitHub).
# Uso:
#   ./scripts/revisao.ps1                          -> execucao normal
#   ./scripts/revisao.ps1 -Provocar teste          -> faz um teste falhar de proposito
#   ./scripts/revisao.ps1 -Provocar analisador     -> cria codigo que viola uma regra do .editorconfig
param(
    [ValidateSet("nenhuma", "teste", "analisador")]
    [string]$Provocar = "nenhuma",
    [int]$Repeticoes = 3
)

Set-Location (Join-Path $PSScriptRoot "..")
$violacao = "Relatorio008/ViolacaoProposital.cs"

function Executar($titulo, $comando) {
    Write-Host ""
    Write-Host "=== $titulo ==="
    & $comando
    if ($LASTEXITCODE -ne 0) {
        Write-Host "REVISAO FALHOU na etapa: $titulo" -ForegroundColor Red
        exit 1
    }
}

try {
    if ($Provocar -eq "analisador") {
        # campo privado nunca usado -> IDE0051 (erro no .editorconfig)
        Set-Content $violacao "public class ViolacaoProposital`n{`n    private int _naoUsado;`n}`n"
    }
    if ($Provocar -eq "teste") {
        $env:PROVOCAR_FALHA_TESTE = "1"
    }

    Executar "Restore" { dotnet restore }
    Executar "Build (analisadores)" { dotnet build --no-restore --configuration Release }
    Executar "Formatacao" { dotnet format --verify-no-changes --no-restore }

    # roda varias vezes, cada uma com uma ordem aleatoria diferente
    for ($i = 1; $i -le $Repeticoes; $i++) {
        $env:SEMENTE_TESTES = Get-Random -Maximum 100000
        Executar "Testes $i/$Repeticoes (semente $($env:SEMENTE_TESTES))" {
            dotnet test --no-build --configuration Release `
                --logger "trx;LogFileName=resultados$i.trx" `
                --logger "console;verbosity=normal" `
                --results-directory TestResults
        }
    }

    Write-Host ""
    Write-Host "REVISAO OK. Resultados em TestResults/" -ForegroundColor Green
}
finally {
    Remove-Item $violacao -ErrorAction SilentlyContinue
    Remove-Item Env:PROVOCAR_FALHA_TESTE -ErrorAction SilentlyContinue
}
