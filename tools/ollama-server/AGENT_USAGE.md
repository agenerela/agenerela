# Using the Ollama server: guide for an AI agent

For a Claude Code, Codex or other agent session on **a machine that runs models on a server
instead of its own GPU**: usually the lead's laptop, at home or over the VPN. It covers connecting,
checking the server, running the live tests and the model-sweep kit against it, and what each
failure means. Read all of it before your first request.

On the RTX 5080 machine itself the same steps work, with the server at `http://127.0.0.1:11436`.
Setting that server up is in [README.md](README.md), and is the lead's job on that machine.

## Two servers

The same script, variables and Unity settings reach either of two servers. `AGENERELA_OLLAMA_URL`
decides which, and `check` prints it on its first line, so run `check` before anything else and
read that line.

| | The RTX 5080 machine | The home server |
|---|---|---|
| Address | its LAN address on port 11436, such as `http://192.168.1.129:11436` | `http://192.168.1.4:11436` |
| Card | RTX 5080, 16 GB | RTX 4070 SUPER, 12 GB |
| Ollama | `0.40.14` in Docker | `0.40.14` in Docker |
| In front of it | the gateway in this folder | a gate that queues every request, and gives the card to Ollama or to ComfyUI, one at a time |
| Who else uses it | the asset server, the lead's own ComfyUI, the run queue | the asset server's jobs on the same machine, and other people at home, each with their own key |
| Set up from | [README.md](README.md) | its own repository, not this one |

**Since 21 November 2026 the lead's laptop points at the home server.** The RTX 5080 machine is
still where the run queue plays requests
([`tools/live-runs/runner/AGENT_USAGE.md`](../live-runs/runner/AGENT_USAGE.md)).

Everything below was written for the RTX 5080 machine and holds for the home server too, except
where [On the home server](#on-the-home-server) says otherwise. Read that section whenever `check`
prints `192.168.1.4`.

## What is on the other end

Ollama `0.40.14` in Docker on the RTX 5080 (16 GB), behind a gateway on port 11436 that wants a
key on every request. It answers Ollama's own HTTP API, so the framework's `OllamaProvider`, the
live tests and `tools/live-runs/` talk to it directly once they have its address and key. For
everything else there is one script, `tools/ollama-server/ollama_remote.py` (Python 3.8+, nothing
to install):

| Command | Does |
|---|---|
| `check` | the server's version, the models pulled, the models in memory |
| `ps` | the models in memory, and how much of each is on the GPU |
| `warm <model>` | loads a model now and says how long it took |
| `unload <model>`, `unload --all` | unloads and waits until the GPU is free of it |
| `pull <model>` | has the server download a model |
| `systemone <model>` | asks a decision model one question on `/v1/systemone`, to check that route works |

You cannot start, stop or inspect the server from here, read its logs, or delete a model, and you
never need to.

## Rules

1. **The key is secret.** It lives in `tools/ollama-server/.env`, and on Windows also in the user's
   `AGENERELA_OLLAMA_API_KEY` environment variable. Never open, print, `cat` or echo that file, the
   variable or the key; never put the key in a URL, a command line, a commit, a log or a chat; and
   never ask the user to paste it into the chat. `ollama_remote.py` and `sweep_one.sh` read it
   themselves. Claude Code's settings deny reading any `.env`, and that is intended.
2. **Only the gateway.** No `docker`, `ssh` or remote commands aimed at the RTX 5080 machine. It also
   runs the asset server and the lead's own ComfyUI, which nothing from here may touch.
3. **Ask before pulling.** A model is gigabytes on the server's disk. Pull one only when the user
   asked for it or the run they asked for needs it, and say which.
4. **One session at a time, on a quiet GPU** (hard rule 3). First check the run queue on the RTX
   5080 machine: `python tools/live-runs/runner/runq.py status`. While it is running, the card is
   the runner's: pause it (`runq.py pause "<why>"`, after the user's yes) and resume it when you are
   done ([`tools/live-runs/runner/AGENT_USAGE.md`](../live-runs/runner/AGENT_USAGE.md)). Then, on
   the lead's laptop, take the lock in
   `.scratch/ollama-5080-lock/` before any request, even `check`, and release it when done; its
   README has the commands. It covers sessions on the laptop only, so also run `ps`: if a model you
   did not load is resident, someone else is using the card: ask the user. Unload what you loaded
   when you are done.
5. **This server is its own machine for measuring.** It runs Ollama's Linux build in Docker, not the
   Ollama app, so never compare a result from it with one from another Ollama, even on the same
   card. Record "RTX 5080, Docker Ollama 0.40.14, over the network (home or VPN)" with every result.
6. **Report failures; don't retry them in a loop.** The table at the end says what each means.

## First time on a machine

1. Get the repository's current `test` branch.
2. Check for the settings without opening them:

   ```bash
   python tools/ollama-server/ollama_remote.py check
   ```

   `error: set AGENERELA_OLLAMA_URL and AGENERELA_OLLAMA_API_KEY ...` means `.env` is missing or
   incomplete. Ask the user to create it themselves:
   - copy `tools/ollama-server/.env.example` to `tools/ollama-server/.env`;
   - set `AGENERELA_OLLAMA_API_KEY` to the value in the server's `.env`, carried over by USB stick or
     a password manager;
   - set `AGENERELA_OLLAMA_URL` to the server's address on port 11436, which `setup.ps1` printed
     there, such as `http://192.168.1.129:11436`. An IP address, never a machine name. Away from
     home, the VPN must be connected; the address is the same.
3. Expect, from `check`:

   ```
   server    http://192.168.1.129:11436
   ollama    0.40.14
   pulled    1: qwen3.5:2b
   resident  none
   ```

   A note after the version, `(docker-compose.yml pins X ...)`, means the server and this checkout
   disagree on the version: tell the user before measuring anything.
4. **For Unity**, check that the key is in the environment, without printing it:

   ```bash
   powershell -NoProfile -Command "[bool][Environment]::GetEnvironmentVariable('AGENERELA_OLLAMA_API_KEY','User')"
   ```

   `False` means the user has not set it. Ask them to run the two PowerShell lines in
   [README.md, "Unity on the laptop"](README.md#unity-on-the-laptop) themselves, then to quit and
   restart Unity and Unity Hub.

## Running against it

**The model-sweep kit** ([`tools/live-runs/model-sweep/`](../live-runs/model-sweep/README.md)).
Follow its README, with two differences. Leave `OLLAMA_URL` unset: `sweep_one.sh` then reads the
address and the key from `tools/ollama-server/.env` and says `(with its key)` on its first line.
And step 4's "template selection" line is in the server's log, which you cannot read: ask the user
to run this on the RTX 5080 machine and give you the lines:

```bash
docker logs agenerela-ollama 2>&1 | grep "template selection"
```

Before the first model, `warm` it once if the server was just restarted or the model just pulled:
the first load then reads the file from disk and took 54 s, past the framework's 30 s timeout.

**The `RequiresOllama` tests**, in Unity batch mode or the Test Runner, read `AGENERELA_OLLAMA_URL`
and `AGENERELA_OLLAMA_API_KEY` from the environment. A batch-mode run started from Git Bash gets
them by being started through `sweep_one.sh`, or from a shell where the user set them. A 401 fails
them on purpose, naming the variable to set.

**A game or a scene in the Editor**: an `OllamaProviderConfig` with **Endpoint** set to the
server's address and **API Key Variable** set to `AGENERELA_OLLAMA_API_KEY`. That config is for
this machine: don't commit a shared config pointed at the 5080 without the user's say-so.

**Your own probe or script**: send `Authorization: Bearer <key>` with the key read from the
environment or the `.env` by the script, never written into the script or its command line.
`tools/live-runs/model-sweep/probe.py` is a short example.

## On the home server

What differs when `check` prints `http://192.168.1.4:11436`. Checked on 21 November 2026, from the
lead's laptop at home.

**Look without a key.** The gate has a status page at `http://192.168.1.4:8291/`, and the same data
as JSON at `/status.json`. It says who holds the card, what is running, who is waiting, the VRAM in
use and the last problems. Read it before a run, and when a request fails.

**The gate does the queueing, so rule 4 changes.** The run queue and the lock in
`.scratch/ollama-5080-lock/` belong to the RTX 5080 machine; neither applies here. The gate puts one
request on the card at a time and makes the rest wait, and it unloads Ollama by itself when a
ComfyUI job needs the card. Two sessions cannot break each other's requests, but they still take
turns, so:

- A check that only has to pass or fail needs nothing first.
- **Anything timed or measured takes a clean lease first and releases it after** (hard rule 3). A
  lease gives one client the whole card, unloaded, and every other client waits. The lead's
  `degenerator` skill has `degen_lease.py`, which takes the lease, runs a command and always
  releases it. A session without that skill asks the user rather than timing on a shared card.
- Still run `ps` first, and unload what you loaded when you are done.

**Models.** 15 are pulled (21 November): the ten in Living Vale's model list, which include
`qwen3.5:2b` and `qwen3.5:4b`, three more from run request 001 and the two from the 4B candidates
A/B. Each loads wholly on the
12 GB card; the largest, `qwen3:8b`, takes 5.6 GB. `check` lists them. Every model loads with a
16,384-token context unless the request asks for another. No decision model is pulled, so
`systemone` has nothing to ask: with any other model it answers HTTP 400, which still shows the
route is open.

**The first load of a model** after a pull or a restart took 3 to 19 s here, and can take about a
minute when the server has to read the file from disk. `warm` it before a run, as on the RTX 5080.

**Measuring (rule 5).** This is a third machine: never compare a result from it with one from the
RTX 5080 or from the laptop's own Ollama. Record "RTX 4070 SUPER 12 GB, Docker Ollama 0.40.14,
behind the gate, over the home network", the gate's commit from the status page, and that the run
held a clean lease.

**The `RequiresOllama` tests** ran here in Unity batch mode on 21 November: 8 of 9 passed. The
Phase 3 suite scored 17/20 with the guards on and 17/20 without, below its bar of 19/20, on
`qwen3.5:2b` (Q8_0, digest `0689d44085e0`). That was one run without a lease, so it is not a
measurement, and it is not in `findings.md`. Expect that test to fail here until someone looks
into it.

**The server's log** is read by the user, on the server. The model-sweep kit's "template selection"
line is in the log of the `degenerator-ollama` container there; `logs.py` is the RTX 5080
machine's only.

**Failures read differently**, because `ollama_remote.py` words them for the RTX 5080's gateway:

| What you see | Means here | What to do |
|---|---|---|
| `the gateway is up but Ollama is not answering` | Either a 503: the card is busy, and your turn did not come within 120 s. Or a 502: Ollama is restarting | Read the status page. If someone holds the card, wait for them, then try once more; never retry in a loop |
| `the gateway rejected the key` | The key here is not the `agenerela` client's key on the gate, or was replaced there | Ask the user to set it again themselves, in `.env` and in the user variable. Never ask for the key |
| `cannot reach ...` | The home server is off, or this machine is not on the home network or the VPN | Ask the user |
| `... is not exposed through the gateway` | A route the gate never passes, such as deleting a model | Report it; that is done on the server, by the user |
| a check passes with `.env` but Unity gets a 401, or reaches the old server | The user variables `AGENERELA_OLLAMA_URL` and `AGENERELA_OLLAMA_API_KEY` override `.env`, and a running Unity keeps the values it started with | Compare the two without printing the key; ask the user to fix the variable, then restart Unity and Unity Hub |

## When something goes wrong

On the RTX 5080 machine. The home server's table is in the section above.

| What you see | Meaning | What to do |
|---|---|---|
| `error: set AGENERELA_OLLAMA_URL and AGENERELA_OLLAMA_API_KEY` | no `.env`, or a line is empty | "First time on a machine", step 2 |
| `cannot reach ...`, or the framework's `Unreachable` | the RTX 5080 machine is off, asleep or rebooted, Docker Desktop is not running there, the VPN is down, or the address is wrong | Ask the user to check the machine and, after a reboot, to start Docker Desktop; the stack comes back by itself. Away from home, check the VPN |
| `the gateway rejected the key`, a `Rejected` with HTTP 401, or `sweep_one.sh` stopping with exit 6 | the key here differs from the server's, or Unity was started before the variable was set | Ask the user to copy the key again, or to restart Unity and Unity Hub |
| `API Key Variable names AGENERELA_OLLAMA_API_KEY, but that environment variable is not set here` | Unity cannot see the variable | As above: set it, then restart Unity and Unity Hub |
| `the gateway is up but Ollama is not answering` (HTTP 502) | the Ollama container is starting, or stopped | Wait a minute and `check` again; if it persists, tell the user |
| `... is not exposed through the gateway` (HTTP 403) | a route the gateway blocks, such as deleting a model | Report it; it is done on the server, by the user |
| `Timeout` on the first decision | the model's first load after a restart or a pull | `warm` the model, then run again |
| `ps` shows a model you did not load | someone else is using the GPU | Ask the user before timing anything |
| `ModelNotFound` | the server has not pulled that model | Ask the user whether to `pull` it (rule 3) |

For anything else, report the exact message. The server's logs are only on the RTX 5080 machine:
to see what the server made of a request, ask the user to run
`python tools/ollama-server/logs.py --problems` there (or `--since 10m` for everything) and paste
you the lines. It never shows the key.
