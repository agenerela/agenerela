#!/usr/bin/env bash
# One model of the example-turns A/B (tools/live-runs/example-turns/README.md, #151): checks, then
# the block/turns A/B through EvalRunner, in Unity batch mode, then unload. Run it from Git Bash,
# one model at a time.
#
# Usage: run_one.sh <model>
# Set first (export ...):
#   RUN_OUT         folder for this run's files, outside the repository's tracked folders
#   UNITY_PROJECT   the detached copy's UnityProject folder, on a short path
#   SWEEP_SET       set file under Assets/ (Evaluation/Interim/agent.json for the interim set)
# Optional: OLLAMA_URL, AGENERELA_OLLAMA_API_KEY, UNITY_EXE, OLLAMA_VERSION, as sweep_one.sh reads them;
#   OLLAMA_ENV_FILE  the tools/ollama-server/.env to read them from, when this copy has none (a worktree)
set -u
M="$1"
: "${RUN_OUT:?set RUN_OUT}" "${UNITY_PROJECT:?set UNITY_PROJECT}" "${SWEEP_SET:?set SWEEP_SET}"
UNITY_EXE="${UNITY_EXE:-/c/Program Files/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe}"
WANT="${OLLAMA_VERSION:-0.40.14}"
HERE="$(cd "$(dirname "$0")" && pwd)"
ENV_FILE="${OLLAMA_ENV_FILE:-$HERE/../../ollama-server/.env}"
from_env() { [ -f "$ENV_FILE" ] && sed -n "s/^$1=//p" "$ENV_FILE" | tr -d '\r' | head -1; }
ENV_URL=$(from_env AGENERELA_OLLAMA_URL)
API="${OLLAMA_URL:-${ENV_URL:-http://127.0.0.1:11434}}"
# The key goes only to the address it belongs to.
if [ -z "${AGENERELA_OLLAMA_API_KEY:-}" ] && [ -n "$ENV_URL" ] && [ "$API" = "$ENV_URL" ]; then
  AGENERELA_OLLAMA_API_KEY=$(from_env AGENERELA_OLLAMA_API_KEY)
fi
export AGENERELA_OLLAMA_API_KEY="${AGENERELA_OLLAMA_API_KEY:-}"
# curl with the key in a config read from stdin, so it never appears on a command line.
api() {
  if [ -n "$AGENERELA_OLLAMA_API_KEY" ]; then
    printf 'header = "Authorization: Bearer %s"\n' "$AGENERELA_OLLAMA_API_KEY" | curl -K - "$@"
  else
    curl "$@"
  fi
}
SLUG=$(echo "$M" | sed 's#[/:]#_#g')
mkdir -p "$RUN_OUT"; cd "$RUN_OUT"
OUT_WIN=$(cd "$RUN_OUT" && pwd -W 2>/dev/null || pwd)

echo "=== $M  $(date +%H:%M:%S)  server $API${AGENERELA_OLLAMA_API_KEY:+ (with its key)}"
V=$(api -s -m 5 "$API/api/version"); echo "version $V"
case "$V" in *"Missing or wrong token"*) echo "STOP: the gateway refused the key (AGENERELA_OLLAMA_API_KEY)"; exit 6;; esac
case "$V" in *"\"$WANT\""*) ;; *) echo "STOP: the server is not Ollama $WANT"; exit 2;; esac
B=$(powershell -NoProfile -Command "(Get-CimInstance Win32_Battery).BatteryStatus" | tr -d '\r')
[ -z "$B" ] || [ "$B" = "2" ] || { echo "STOP: on battery (BatteryStatus $B)"; exit 3; }
# Editors only: the Unity CLI's own daemon (Unity Hub\resources\unity.exe serve) opens no project.
U=$(powershell -NoProfile -Command "(Get-CimInstance Win32_Process -Filter \"Name='Unity.exe'\" | Where-Object { \$_.ExecutablePath -like '*\Editor\Unity.exe' } | Measure-Object).Count" | tr -d '\r')
[ "$U" = "0" ] || { echo "STOP: $U Unity editor(s) already running here"; exit 4; }
PS=$(api -s -m 5 "$API/api/ps"); echo "ps before $PS"
case "$PS" in *'"models":[]'*) ;; *) echo "STOP: a model is already resident on the server"; exit 5;; esac

START=$(date +%s)
AGENERELA_OLLAMA_URL="$API" AGENERELA_OLLAMA_MODEL="$M" AGENERELA_SWEEP_SET="$SWEEP_SET" \
  "$UNITY_EXE" -batchmode -nographics -projectPath "$UNITY_PROJECT" -runTests -testPlatform PlayMode \
  -testFilter "Agenerela.Tests.ExampleTurnsRun" -testResults "$OUT_WIN/ab-$SLUG.xml" -logFile "$OUT_WIN/ab-$SLUG-unity.log"
echo "unity exit $? after $(( $(date +%s) - START )) s"
grep -o 'result="[A-Za-z]*"' "ab-$SLUG.xml" | head -1
grep -o "EVAL-TOOK.*" "ab-$SLUG-unity.log" | head -1
N=$(grep -o "EVAL-REPORT [^ ]*" "ab-$SLUG-unity.log" | head -1 | grep -o "[0-9_-]*block_vs_turns")
if [ -n "$N" ]; then
  cp -r "$UNITY_PROJECT/Logs/Evaluation/$N" "report-$SLUG"; echo "report report-$SLUG"
else
  echo "NO REPORT"; grep -n "Timeout value\|Exception\|refuse" "ab-$SLUG-unity.log" | head -5
fi
api -s -m 60 "$API/api/chat" -d "{\"model\":\"$M\",\"keep_alive\":0,\"messages\":[]}" >/dev/null
for i in $(seq 1 30); do api -s -m 5 "$API/api/ps" | grep -q '"models":\[\]' && break; sleep 1; done
echo "ps after $(api -s -m 5 "$API/api/ps")"
echo "=== done $M $(date +%H:%M:%S)"
