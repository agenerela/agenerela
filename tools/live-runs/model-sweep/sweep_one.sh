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
#   OLLAMA_URL      the server, http://127.0.0.1:11434 when unset (another machine: http://<its LAN IP>:11434)
#   UNITY_EXE       the 6000.3.23f1 editor, the Hub's default path when unset
#   PROBE_REQUESTS  how many probe requests, 6 when unset (2 once a model's order is known)
#   OLLAMA_VERSION  the version every model must be served by, 0.40.14 when unset
set -u
M="$1"
: "${SWEEP_OUT:?set SWEEP_OUT}" "${UNITY_PROJECT:?set UNITY_PROJECT}" "${SWEEP_SET:?set SWEEP_SET}"
API="${OLLAMA_URL:-http://127.0.0.1:11434}"
UNITY_EXE="${UNITY_EXE:-/c/Program Files/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe}"
WANT="${OLLAMA_VERSION:-0.40.14}"
HERE="$(cd "$(dirname "$0")" && pwd)"
SLUG=$(echo "$M" | sed 's#[/:]#_#g')
mkdir -p "$SWEEP_OUT"; cd "$SWEEP_OUT"
OUT_WIN=$(cd "$SWEEP_OUT" && pwd -W 2>/dev/null || pwd)

echo "=== $M  $(date +%H:%M:%S)  server $API"
V=$(curl -s -m 5 "$API/api/version"); echo "version $V"
case "$V" in *"\"$WANT\""*) ;; *) echo "STOP: the server is not Ollama $WANT"; exit 2;; esac
# Mains power, on the machine running this script (a desktop has no battery: that counts as mains).
B=$(powershell -NoProfile -Command "(Get-CimInstance Win32_Battery).BatteryStatus" | tr -d '\r')
[ -z "$B" ] || [ "$B" = "2" ] || { echo "STOP: on battery (BatteryStatus $B)"; exit 3; }
U=$(powershell -NoProfile -Command "(Get-Process Unity -ErrorAction SilentlyContinue | Measure-Object).Count" | tr -d '\r')
[ "$U" = "0" ] || { echo "STOP: $U Unity process(es) already running here"; exit 4; }
PS=$(curl -s -m 5 "$API/api/ps"); echo "ps before $PS"
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
curl -s -m 60 "$API/api/chat" -d "{\"model\":\"$M\",\"keep_alive\":0,\"messages\":[]}" >/dev/null
for i in $(seq 1 30); do curl -s -m 5 "$API/api/ps" | grep -q '"models":\[\]' && break; sleep 1; done
echo "ps after $(curl -s -m 5 "$API/api/ps")"
echo "=== done $M $(date +%H:%M:%S)"
