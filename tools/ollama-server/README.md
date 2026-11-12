# Ollama server for live runs

Ollama in Docker on the lead's RTX 5080 machine, served to other machines through a gateway that
wants a key. The laptop, or a teammate's machine, runs Unity, the live tests and the run kits in
[`tools/live-runs/`](../live-runs/) against that card instead of its own. It is built the same
way as the asset server beside it, [`tools/comfyui/`](../comfyui/README.md).

An AI agent on the machine that asks reads [AGENT_USAGE.md](AGENT_USAGE.md) instead of this page.

| | |
|---|---|
| Ollama | `ollama/ollama:0.40.14`, the version the 9 November sweep was measured on, pinned in `docker-compose.yml` |
| Settings | `OLLAMA_GO_TEMPLATE=1`, `OLLAMA_CONTEXT_LENGTH=16384`, `OLLAMA_NO_CLOUD=0`, the ones the model-sweep kit starts `ollama serve` with |
| Gateway | Caddy on port 11436, every request with `Authorization: Bearer <key>` |
| Models | a Docker volume of its own, `agenerela-ollama_models`, apart from the Ollama app's |
| Client helper | `ollama_remote.py`: `check`, `ps`, `warm`, `unload`, `pull`. Python 3.8+, standard library only |
| Logs | `logs.py` on the server: both containers' logs as one short timeline. Docker keeps 5 files of 20 MB per container |

## Why Docker, and what it changes

- **The version cannot drift.** The Ollama app updates itself, and every result is tied to the
  version it was measured on. Here the image tag decides, and only a commit changes it.
- **The settings are written down.** They are in `docker-compose.yml`, not typed before each
  `ollama serve`.
- **It is kept apart from the Ollama app** on the same machine, which keeps port 11434 and its own
  models.
- **It is its own machine for measuring.** It runs Ollama's Linux build under WSL2, not the native
  Windows one, so a result from it is never compared with one from the app, even on the same card.
  A/B runs go both arms on one server, as always ([`measuring.md`](../../docs/llm-wiki/measuring.md)).

## Set up the RTX 5080 machine

Needs Docker Desktop with the WSL2 engine and an NVIDIA driver, as the asset server does.

1. From PowerShell in this folder:

   ```powershell
   powershell -ExecutionPolicy Bypass -File .\setup.ps1
   ```

   It checks Docker, the GPU, the container names and the port, writes `.env` with a new random
   64-character key and `AGENERELA_OLLAMA_URL` pointing at this machine, and prints the addresses
   other machines can use. It starts nothing, and in an existing `.env` it only fills an empty
   `AGENERELA_OLLAMA_URL`.
2. Start the stack:

   ```powershell
   docker compose up -d
   ```

   It comes back by itself after a reboot, once Docker Desktop is running.
3. Pull the models the runs need, on the server itself:

   ```powershell
   docker exec agenerela-ollama ollama pull qwen3.5:2b
   ```

   `ollama_remote.py pull <model>` does the same from another machine.
4. Check it from this machine:

   ```powershell
   python ollama_remote.py check
   ```

   It prints the version, the models pulled and the models in memory. `0.40.14` is the version to
   expect.
5. Carry the `AGENERELA_OLLAMA_API_KEY` line over to the other machine by USB stick or a password
   manager, never through a chat.

**The firewall.** Windows lets Docker Desktop's published ports in through its own *Docker Desktop
Backend* rule, which on this machine already allows private and public networks, so no new rule
was needed. If another machine cannot connect, check that rule exists and that this machine's
network is *Private*. Because the rule also covers public networks, set `OLLAMA_API_BIND` in
`.env` to this machine's LAN address to listen only there; the key is required either way.

## Connect from the laptop

1. In `tools/ollama-server/` on the laptop, copy `.env.example` to `.env` and fill in two lines:
   - `AGENERELA_OLLAMA_API_KEY`: the same value as in the server's `.env`;
   - `AGENERELA_OLLAMA_URL`: one of the addresses `setup.ps1` printed, such as
     `http://192.168.1.129:11436`. Use the IP address, not the machine's name, which can resolve to
     IPv6 first and cost about 2 s a request, as `localhost` does. Away from home, connect the VPN
     first; the address stays the same.
2. Check:

   ```bash
   python tools/ollama-server/ollama_remote.py check
   ```

That is enough for `ollama_remote.py` and the model-sweep kit: `sweep_one.sh` reads both lines from
this `.env` when `OLLAMA_URL` is not set ([its README](../live-runs/model-sweep/README.md)).

### Unity on the laptop

Unity reads the key from an environment variable, never from a file or an asset. Set it once, for
your Windows user, in PowerShell from the repository's root:

```powershell
$line = Select-String -Path tools\ollama-server\.env -Pattern '^AGENERELA_OLLAMA_API_KEY=(.+)$'
[Environment]::SetEnvironmentVariable('AGENERELA_OLLAMA_API_KEY', $line.Matches[0].Groups[1].Value, 'User')
```

Then quit Unity **and Unity Hub** and start them again: they read their environment when they
start.

- **An `OllamaProviderConfig`**: set **Endpoint** to the server's address and **API Key Variable**
  to `AGENERELA_OLLAMA_API_KEY`. The asset stores the variable's name, never the key. A config
  pointed at the 5080 is for your machine, so make one of your own and leave it out of commits, or
  do not commit the change to a shared one.
- **Code**: `new OllamaSettings { Endpoint = ..., ApiKey = Environment.GetEnvironmentVariable("AGENERELA_OLLAMA_API_KEY") }`.
- **The `RequiresOllama` tests** read `AGENERELA_OLLAMA_URL` and `AGENERELA_OLLAMA_API_KEY`. A server
  that answers 401 fails them, rather than skipping them as an unreachable one does.

## How it is exposed

- **Ollama has no login.** Its own port is published nowhere: only the gateway can reach it.
- **The gateway is the only door.** It answers 401 without the key, and passes only the routes the
  framework, the kits and `ollama_remote.py` use: `/api/version`, `tags`, `ps`, `show`, `chat`,
  `generate`, `embed` and `pull`, and `/v1/systemone`, the route decision models answer on.
  Deleting, creating, copying or pushing a model, blob uploads and every other `/v1` route answer
  403; do those on the server with `docker exec`. Caddy's log
  shows the key as `REDACTED`.
- **Traffic is plain HTTP.** Fine at home and over the VPN, which encrypts it. On a network you
  do not trust, use the VPN or Tailscale, never the open port.
- **Never forward the port on the router.**
- **If the key leaks**, delete the server's `.env`, run `setup.ps1` again, re-apply any port
  changes, run `docker compose up -d`, and update every client's `.env` and environment variable.
- **The machine must be awake.** Windows sleep stops everything.

## Measuring on it

- Record the **server's** card, driver and Ollama version with a result, and that it is this Docker
  server, not the app.
- **Latency includes the network hop**, about a millisecond at home and more over the VPN. Say
  which.
- **The GPU is shared** with both ComfyUI stacks. Before a timed run, check nothing else holds
  memory on it: `nvidia-smi` on the server, and `ollama_remote.py ps` from anywhere. Never stop
  another container to make room without the lead's say-so.
- **Unload before timing** (hard rule 3): `ollama_remote.py unload --all`.
- **One client at a time, and no `OLLAMA_NUM_PARALLEL`.** Ollama 0.40.14 serves `qwen3.5` one
  request at a time whatever the setting, so two sessions only take turns. On models it does run in
  parallel, two requests at once changed 13 of 60 replies and two decisions, so a result from two
  sessions at once is not comparable with one from one (findings, 11 November).
- **The first load after a pull or a restart is slow**: 54 s for `qwen3.5:2b` the first time, while
  Docker read the file from disk, 28.5 s after the containers were recreated, and 1.9 s cold
  otherwise (9 November). The framework's default timeout is
  30 s, so warm a model first: `ollama_remote.py warm <model>`.
- **The model-sweep kit's "template selection" line** is in the server's log only:
  `python logs.py --since 3h | grep load`, on the server, gives each load's model, time and template,
  or `docker logs agenerela-ollama 2>&1 | grep "template selection"` for the raw line.

## Everyday commands

On the server, from this folder:

| Do | Command |
|---|---|
| Start, or apply a change to `docker-compose.yml` | `docker compose up -d` |
| Stop (models stay) | `docker compose stop` |
| State | `docker compose ps` |
| What happened, in short | `python logs.py` (see [Logs](#logs)) |
| Ollama's full log | `docker logs -f agenerela-ollama` |
| The gateway's log | `docker logs -f agenerela-ollama-gateway` |
| Pull, list, remove a model | `docker exec agenerela-ollama ollama pull <model>` (`list`, `rm <model>`) |

From any machine with the `.env`:

| Do | Command |
|---|---|
| Is it up, which version, what is loaded | `python tools/ollama-server/ollama_remote.py check` |
| What is in memory, how much on the GPU | `python tools/ollama-server/ollama_remote.py ps` |
| Load a model before timing it | `python tools/ollama-server/ollama_remote.py warm <model>` |
| Free the GPU | `python tools/ollama-server/ollama_remote.py unload --all` |
| Check a decision model answers on `/v1/systemone` | `python tools/ollama-server/ollama_remote.py systemone <model>` |

## Logs

Both containers log to Docker, which keeps five files of 20 MB each per container and drops the
oldest, so the logs never fill the disk. Recreating a container (`docker compose up -d` after a
change) starts its log afresh.

**Read them with `logs.py`**, on the server, from this folder:

```powershell
python logs.py                  # the last hour
python logs.py --since 6h       # further back: 10m, 2h, 1d
python logs.py -f               # keep printing as things happen; Ctrl+C stops it
python logs.py --problems       # only what went wrong
```

It prints one line per event, in this machine's local time:

```
11-09 16:58:23  start    GPU NVIDIA GeForce RTX 5080, 15.9 GiB total, 14.6 GiB available
11-09 17:04:37  load     qwen3.5:2b in 35.7 s, template renderer_parser, 14.6 GiB GPU free before
11-09 17:12:37  problem  401       0 ms  POST  /api/generate  wrong or missing key  (via 192.168.1.129)
11-09 17:36:46  request  200       1 ms  GET   /api/ps  (via 192.168.1.129)
```

- **request** is every request the gateway handled: status, duration, method and path. `via
  <address>` means another machine called the LAN address; none means this machine. Docker Desktop
  hides the caller's own address, so that is all the log can say about who called.
- **load** is a model loading: which, in how long, its chat template (the sweep kit's "template
  selection" line) and the GPU memory free before it.
- **pull** and **start** are downloads and the server starting.
- **problem** is a refused request (401 wrong key, 403 a blocked route), a 5xx, or any warning or
  error from Ollama or llama.cpp.

`--verbose` adds Ollama's other lines. llama.cpp's detail, about 25 lines a request and a few
hundred a load, is only in `docker logs agenerela-ollama`: Ollama starts llama.cpp at its highest
verbosity, and no setting turns that down. The gateway's own log is already one short line per
request; the key never appears in it.

## Upgrading Ollama

Change the image tag in `docker-compose.yml` in a pull request of its own, then on the server
`docker compose up -d`. In the same pull request, raise `OLLAMA_VERSION` in
`tools/live-runs/model-sweep/sweep_one.sh`, re-run `tools/benchmarks/schema_visibility_probe.py`
against it (AGENTS.md), and record in `findings.md` that results before and after are on different
versions.

## Troubleshooting

| `ollama_remote.py` says | Meaning | Fix |
|---|---|---|
| `error: set AGENERELA_OLLAMA_URL and AGENERELA_OLLAMA_API_KEY` | no `.env`, or a line is empty | [Connect from the laptop](#connect-from-the-laptop) |
| `cannot reach ...` | the server is off or asleep, Docker Desktop is not running, the stack is down, the VPN is off, or the address is wrong | Check each, in that order |
| `the gateway rejected the key` | the two `.env` files hold different keys | Copy the key over again |
| `the gateway is up but Ollama is not answering` | the `ollama` container is starting or stopped | `docker compose ps`, then `python logs.py --problems` on the server |
| `... is not exposed through the gateway` | a route the gateway blocks | Do it on the server with `docker exec` |
| `(docker-compose.yml pins X: results are not comparable)` | the server runs another version than this checkout pins | Bring the server and the checkout to the same commit |
