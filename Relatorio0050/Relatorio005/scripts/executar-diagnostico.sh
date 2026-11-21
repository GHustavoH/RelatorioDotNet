#!/usr/bin/env bash
set -euo pipefail

EXPERIMENTO="${EXPERIMENTO:-experimento.json}"
PASTA_BASE="${PASTA_BASE:-resultados}"
TRACE_DURACAO_S="${TRACE_DURACAO_S:-20}"
SEM_COUNTERS="${SEM_COUNTERS:-0}"
SEM_TRACE="${SEM_TRACE:-0}"
EXECUTAR_BENCHMARK="${EXECUTAR_BENCHMARK:-0}"

RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$RAIZ"

PASTA="$PASTA_BASE/$(date +%Y%m%d-%H%M%S)"
mkdir -p "$PASTA"

dotnet --info > "$PASTA/dotnet-info.txt"
cp "$EXPERIMENTO" "$PASTA/experimento.json"

dotnet build Relatorio005.slnx -c Release

API_DLL="$RAIZ/Relatorio005/bin/Release/net10.0/Relatorio005.dll"
LOAD_DLL="$RAIZ/Relatorio005.LoadTest/bin/Release/net10.0/Relatorio005.LoadTest.dll"

VARIAVEIS="$(dotnet "$LOAD_DLL" variaveis "$EXPERIMENTO")"
URL="$(echo "$VARIAVEIS" | grep '^LT_URL=' | cut -d= -f2-)"
AQUECIMENTO="$(echo "$VARIAVEIS" | grep '^LT_AQUECIMENTO_S=' | cut -d= -f2-)"
MEDICAO="$(echo "$VARIAVEIS" | grep '^LT_MEDICAO_S=' | cut -d= -f2-)"
DURACAO_COLETA=$((AQUECIMENTO + MEDICAO + 10))

rodada() {
  local rotulo="$1" estrategia="$2"

  while IFS= read -r linha; do
    case "$linha" in Hub__*) export "$linha" ;; esac
  done <<< "$VARIAVEIS"

  export Hub__Estrategia="$estrategia"
  export ASPNETCORE_ENVIRONMENT=Production
  export ASPNETCORE_URLS="$URL"
  export Logging__LogLevel__Default=Warning

  dotnet "$API_DLL" > "$PASTA/$rotulo-api.log" 2>&1 &
  local api_pid=$!
  trap 'kill "$api_pid" 2>/dev/null || true' RETURN

  local pronto=0
  for _ in $(seq 1 60); do
    if curl -fs "$URL/health" > /dev/null; then pronto=1; break; fi
    sleep 0.5
  done
  [ "$pronto" = "1" ] || { echo "A API nao respondeu em $URL"; return 1; }

  local pid_contadores="" pid_trace=""

  if [ "$SEM_COUNTERS" != "1" ]; then
    dotnet-counters collect --process-id "$api_pid" \
      --counters "System.Runtime,Microsoft.AspNetCore.Hosting,Relatorio005.Hub" \
      --format csv --output "$PASTA/$rotulo-counters.csv" \
      --refresh-interval 1 --duration "$(printf '%02d:%02d:%02d' $((DURACAO_COLETA/3600)) $((DURACAO_COLETA%3600/60)) $((DURACAO_COLETA%60)))" \
      > "$PASTA/$rotulo-counters.log" 2>&1 &
    pid_contadores=$!
  fi

  if [ "$SEM_TRACE" != "1" ]; then
    (
      sleep $((AQUECIMENTO + 5))
      dotnet-trace collect --process-id "$api_pid" \
        --profile dotnet-common,dotnet-sampled-thread-time \
        --duration "$(printf '00:%02d:%02d' $((TRACE_DURACAO_S/60)) $((TRACE_DURACAO_S%60)))" \
        --output "$PASTA/$rotulo.nettrace" > "$PASTA/$rotulo-trace.log" 2>&1
    ) &
    pid_trace=$!
  fi

  dotnet "$LOAD_DLL" --experimento "$EXPERIMENTO" --rotulo "$rotulo" --saida "$PASTA"

  [ -n "$pid_contadores" ] && wait "$pid_contadores" || true
  [ -n "$pid_trace" ] && wait "$pid_trace" || true

  if [ -f "$PASTA/$rotulo.nettrace" ]; then
    dotnet-trace convert "$PASTA/$rotulo.nettrace" --format Speedscope || true
  fi

  kill "$api_pid" 2>/dev/null || true
  wait "$api_pid" 2>/dev/null || true
  trap - RETURN
}

rodada antes Baseline
rodada depois Otimizada

CODIGO_TESTES=0
dotnet test Relatorio005.Tests -c Release --logger "trx;LogFileName=testes.trx" --results-directory "$PASTA" || CODIGO_TESTES=$?

if [ "$EXECUTAR_BENCHMARK" = "1" ]; then
  dotnet run -c Release --project Relatorio005.Benchmarks -- --filter "*" --artifacts "$PASTA/benchmarkdotnet"
fi

dotnet "$LOAD_DLL" comparar "$PASTA/antes.json" "$PASTA/depois.json" | tee "$PASTA/comparacao.txt"

echo "Evidencias em: $PASTA"
exit "$CODIGO_TESTES"
