# Setting up the ComfyUI server: runbook for an AI agent

For a Claude Code, Codex or other agent session running **on the RTX 5080 machine**, with
this repository checked out there. [README.md](README.md) explains the same steps for a
person; this page is the exact sequence, with the rules that keep the user's own ComfyUI
safe. Read all of it before running anything.

## Ground rules

This machine already runs the user's own ComfyUI, possibly in its own Docker container.
This stack has to sit beside it without touching it.

1. **Touch only this stack.** It owns exactly these, and nothing else:

   | Kind | Name |
   |---|---|
   | Compose project | `agenerela-comfyui` |
   | Containers | `agenerela-comfyui`, `agenerela-comfyui-gateway` |
   | Image | `agenerela-comfyui:v0.39.1` |
   | Volume | `agenerela-comfyui_models` |
   | Network | `agenerela-comfyui_default` |
   | Files | this folder, `tools/comfyui/` |

   Never stop, restart, remove, rename, recreate, update or reconfigure any other container,
   image, volume or network, and never edit files outside this folder.
2. **Never run any of these.** Each one can stop or delete the user's other ComfyUI:
   - `docker system prune`, `docker container prune`, `docker image prune`,
     `docker volume prune`, `docker network prune`;
   - `docker stop`, `kill`, `restart`, `rm`, `update` or `rename` on a container not in the
     table;
   - `docker compose` anywhere but this folder, or with `-p` or `-f` pointing elsewhere;
   - `wsl --shutdown`, `--terminate`, `--update` or `--unregister`; quitting, restarting,
     updating or resetting Docker Desktop, or changing its settings; installing or updating
     the NVIDIA driver.

   If one of them looks necessary, stop, and ask the user, saying what it would interrupt.
3. **Reading is always fine:** `docker ps -a`, `docker images`, `docker volume ls`,
   `docker inspect`, `nvidia-smi`, and `docker compose ps`, `logs` or `config` in this folder.
4. **Keep the token secret.** Never print, echo, `cat`, log or paste `COMFY_API_TOKEN` or the
   `.env` file, and never put the token in a URL or a commit. Tell the user where it is.
5. **Change only this stack's settings.** If a port clashes, change `COMFY_UI_PORT` or
   `COMFY_API_PORT` in this folder's `.env`, never the other container. Never publish the
   web UI on anything but `127.0.0.1`.
6. **Large downloads need the user's go-ahead**, unless they asked you for the whole setup:
   the build pulls several GB, and the models are about 33 GB.
7. **Firewall rules are the user's to make.** If one is needed, give them the command from
   the README; do not create it yourself.
8. Install no custom nodes and leave the workflows alone during setup. A fix found in step 8
   goes through a pull request into `test`, following [AGENTS.md](../../AGENTS.md).

## Steps

Run every command from this folder (`tools\comfyui`). They are written for PowerShell; in
Git Bash they are the same except where noted. Steps 4 and 6 take a long time: run them in
the background and check on them, rather than blocking on one call.

If `tools/comfyui/` is missing from the checkout, the branch is out of date. Ask the user
before switching branches or pulling over their local changes.

**0. Record the starting state**, so you can show at the end that nothing else changed:

```powershell
docker ps -a
```

If `docker` is missing, or Docker Desktop is not running, stop and ask the user. Installing
needs an administrator and a restart, and starting Docker Desktop also starts their other
containers.

**1. Check the driver:**

```powershell
nvidia-smi
```

`CUDA Version` (`CUDA UMD Version` on newer drivers) must be 13.0 or higher. If it is lower,
stop: the user updates the driver.

**2. Run the setup check:**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\setup.ps1
```

(Git Bash: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File ./setup.ps1`.) It must end
with `Ready`. It lists the user's other ComfyUI containers; name them in your report. On a
port FAIL, choose free ports for `COMFY_UI_PORT` or `COMFY_API_PORT` in `.env` and run it
again. On a container-name FAIL, stop and ask the user. The script creates `.env` with the
token the first time and never overwrites it afterwards.

**3. Check that Docker can use the GPU:**

```powershell
docker run --rm --gpus all nvidia/cuda:13.0.3-base-ubuntu24.04 nvidia-smi
```

It must show the RTX 5080. If it fails, stop and report: the fixes restart WSL or Docker
Desktop (rule 2).

**4. Build the image** (long; background):

```powershell
docker compose build
```

**5. Check that the image sees the GPU:**

```powershell
docker compose run --rm comfyui python -c "import torch; print(torch.cuda.get_device_name(0))"
```

It must print `NVIDIA GeForce RTX 5080`.

**6. Download the models** (about 33 GB; background). Running it again resumes:

```powershell
docker compose run --rm comfyui python /opt/agenerela/download_models.py
```

It ends with `all done`.

**7. Start the stack:**

```powershell
docker compose up -d
```

Poll `docker compose ps` until `agenerela-comfyui` reports `healthy`; its first start can
take a few minutes. If it restarts in a loop, read `docker compose logs --tail 200 comfyui`.

**8. Check the workflows against the real server.** This runs `comfy.py` inside the
container, talking to ComfyUI directly, so it needs neither Python on the host nor the token
(`local` is a placeholder; ComfyUI ignores it):

```powershell
docker compose exec -e COMFY_URL=http://127.0.0.1:8188 -e COMFY_API_TOKEN=local comfyui python /opt/agenerela/comfy.py validate
```

Every workflow must print `ok`. Then make one of each, smallest first. Each prints the path
of what it saved:

```powershell
docker compose exec -e COMFY_URL=http://127.0.0.1:8188 -e COMFY_API_TOKEN=local comfyui python /opt/agenerela/comfy.py run sfx --set "prompt=Short wooden knock on a door, dry room, close-up. Length: 2 seconds" --set seconds=2 --out /tmp/selftest
```

```powershell
docker compose exec -e COMFY_URL=http://127.0.0.1:8188 -e COMFY_API_TOKEN=local comfyui python /opt/agenerela/comfy.py run image --set "prompt=A weathered wooden barrel with iron bands, game asset, centred, whole object visible, three-quarter view, plain white background, soft even light" --out /tmp/selftest
```

```powershell
docker compose exec -e COMFY_URL=http://127.0.0.1:8188 -e COMFY_API_TOKEN=local comfyui python /opt/agenerela/comfy.py run mesh --set image=/tmp/selftest/<the .png printed above> --set faces=5000 --out /tmp/selftest
```

The same files also land in `data\output\agenerela\` on this machine, for the user to look at
and listen to. If `validate` or a run fails, read its message and
`docker compose logs --tail 200 comfyui`, and see "When validate complains" in the README:
compare against the official template in the web UI at `http://127.0.0.1:8288`. Report what
you found; a change to `workflows/` goes through a pull request.

**9. Check the gateway from the host** (use the port from `.env` if you changed it). Without
the token it must answer 401:

```powershell
curl.exe -s -o NUL -w "%{http_code}" http://127.0.0.1:8289/system_stats
```

With the token it must answer 200, and the web UI's path must answer 403. This line reads the
token into a variable without printing it:

```powershell
$t = (Select-String -LiteralPath .env -Pattern '^COMFY_API_TOKEN=(.+)$').Matches[0].Groups[1].Value.Trim(); curl.exe -s -o NUL -w "%{http_code} " -H "Authorization: Bearer $t" http://127.0.0.1:8289/system_stats; curl.exe -s -o NUL -w "%{http_code}" -H "Authorization: Bearer $t" http://127.0.0.1:8289/; Remove-Variable t
```

Expect `200 403`. (Git Bash: `t=$(sed -n 's/^COMFY_API_TOKEN=//p' .env | tr -d '\r')`, then
the same two `curl` calls with `-o /dev/null` instead of `-o NUL`, then `unset t`.)

**10. Confirm nothing else changed:**

```powershell
docker ps -a
```

Compare with step 0. Every other container must have the same status, and a running one must
not have restarted (its *Up* time keeps growing). Report any difference.

**11. Report to the user:**

- the addresses `setup.ps1` printed, one of which goes into `COMFY_URL` on the machine that
  runs Claude Code or Codex;
- that the token is the `COMFY_API_TOKEN` line of `tools\comfyui\.env` on this machine, for
  them to copy into `tools/comfyui/.env` on that machine by USB stick or password manager;
- the results of steps 8 and 9, with the paths of the files generated in step 8;
- their other ComfyUI containers, named, and unchanged;
- that if the other machine cannot connect, the Windows firewall may need the rule in the
  README, which they create themselves.

## Afterwards

Stop and start only this stack, only from this folder: `docker compose stop` and
`docker compose up -d`. `docker compose down` removes this stack's containers;
`docker compose down -v` also deletes its models volume (33 GB to download again), so ask
the user before running it.
