#!/usr/bin/env bash
# One model of a model sweep (tools/live-runs/model-sweep/README.md): checks, probe, then the
# idle on/off A/B through EvalRunner, in Unity batch mode. Run it from Git Bash, one model at a time.
#
# Usage: sweep_one.sh <model>
# Set first (export ...):
#   SWEEP_OUT       folder for this sweep's files, outside the repository's tracked folders
#   UNITY_PROJECT   the detached copy's UnityProject folder, on a short path (C:/Unity_Projects/wt-live/UnityProject)
#   SWEEP_SET       set file under Assets/ (Evaluation/agent.json; Evaluation/Interim/agent.json for the interim set)
# Optional:
#   OLLAMA_URL      the server: tools/ollama-server/.env's AGENERELA_OLLAMA_URL when this checkout has one,
#                   else http://127.0.0.1:11434
#   AGENERELA_OLLAMA_API_KEY  the key of a server behind tools/ollama-server/'s gateway; read from that
#                   .env when OLLAMA_URL is its address. Sent in headers only, never printed
#   OLLAMA_SERVER_ENV  that .env, when this copy of the kit is in a worktree without one
#                   (another checkout's tools/ollama-server/.env); the file is read, never printed
#   UNITY_EXE       the 6000.3.23f1 editor, the Hub's default path when unset
#   PROBE_REQUESTS  how many probe requests, 6 when unset (2 once a model's order is known)
#   OLLAMA_VERSION  the version every model must be served by, 0.40.14 when unset
set -u
M="$1"
: "${SWEEP_OUT:?set SWEEP_OUT}" "${UNITY_PROJECT:?set UNITY_PROJECT}" "${SWEEP_SET:?set SWEEP_SET}"
UNITY_EXE="${UNITY_EXE:-/c/Program Files/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe}"
WANT="${OLLAMA_VERSION:-0.40.14}"
HERE="$(cd "$(dirname "$0")" && pwd)"
# The RTX 5080's gateway (tools/ollama-server/README.md), when this checkout has its .env.
ENV_FILE="${OLLAMA_SERVER_ENV:-$HERE/../../ollama-server/.env}"
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
mkdir -p "$SWEEP_OUT"; cd "$SWEEP_OUT"
OUT_WIN=$(cd "$SWEEP_OUT" && pwd -W 2>/dev/null || pwd)

echo "=== $M  $(date +%H:%M:%S)  server $API${AGENERELA_OLLAMA_API_KEY:+ (with its key)}"
V=$(api -s -m 5 "$API/api/version"); echo "version $V"
case "$V" in *"Missing or wrong token"*) echo "STOP: the gateway refused the key (AGENERELA_OLLAMA_API_KEY)"; exit 6;; esac
case "$V" in *"\"$WANT\""*) ;; *) echo "STOP: the server is not Ollama $WANT"; exit 2;; esac
# Mains power, on the machine running this script (a desktop has no battery: that counts as mains).
B=$(powershell -NoProfile -Command "(Get-CimInstance Win32_Battery).BatteryStatus" | tr -d '\r')
[ -z "$B" ] || [ "$B" = "2" ] || { echo "STOP: on battery (BatteryStatus $B)"; exit 3; }
# Editors only: Unity Hub's CLI server ("Unity Hub\resources\unity.exe serve") has the same process name.
U=$(powershell -NoProfile -Command "(Get-Process Unity -ErrorAction SilentlyContinue | Where-Object { \$_.Path -like '*\\Editor\\Unity.exe' } | Measure-Object).Count" | tr -d '\r')
[ "$U" = "0" ] || { echo "STOP: $U Unity Editor(s) already running here"; exit 4; }
PS=$(api -s -m 5 "$API/api/ps"); echo "ps before $PS"
case "$PS" in *'"models":[]'*) ;; *) echo "STOP: a model is already resident on the server"; exit 5;; esac

OLLAMA_URL="$API" python "$HERE/probe.py" "$M" "probe-$SLUG.json" "${PROBE_REQUESTS:-6}" 2>&1 | tee "probe-$SLUG.txt"

START=$(date +%s)
AGENERELA_OLLAMA_URL="$API" AGENERELA_OLLAMA_MODEL="$M" AGENERELA_SWEEP_SET="$SWEEP_SET" \
  "$UNITY_EXE" -batchmode -nographics -projectPath "$UNITY_PROJECT" -runTests -testPlatform PlayMode \
  -testFilter "Agenerela.Tests.ModelSweepRun" -testResults "$OUT_WIN/ab-$SLUG.xml" -logFile "$OUT_WIN/ab-$SLUG-unity.log"
echo "unity exit $? after $(( $(date +%s) - START )) s"
grep -o 'result="[A-Za-z]*"' "ab-$SLUG.xml" | head -1
grep -o "EVAL-TOOK.*" "ab-$SLUG-unity.log" | head -1
N=$(grep -o "EVAL-REPORT [^ ]*" "ab-$SLUG-unity.log" | head -1 | grep -o "[0-9_-]*idle-profile_vs_idle-none")
if [ -n "$N" ]; then
  cp -r "$UNITY_PROJECT/Logs/Evaluation/$N" "report-$SLUG"; echo "report report-$SLUG"
else
  echo "NO REPORT"; grep -n "Timeout value\|Exception\|refuse" "ab-$SLUG-unity.log" | head -5
fi
api -s -m 60 "$API/api/chat" -d "{\"model\":\"$M\",\"keep_alive\":0,\"messages\":[]}" >/dev/null
for i in $(seq 1 30); do api -s -m 5 "$API/api/ps" | grep -q '"models":\[\]' && break; sleep 1; done
echo "ps after $(api -s -m 5 "$API/api/ps")"
echo "=== done $M $(date +%H:%M:%S)"
