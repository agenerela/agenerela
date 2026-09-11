# Demo games

Each demo game is its own Unity project in this folder. Every game loads the framework
straight from this repo, so a framework change reaches all of them with no install step,
and a game that breaks is telling you the API needs work. Why separate projects rather
than one: DR-010 in the [build plan](../docs/FRAMEWORK_BUILD_PLAN.md). The rules for demos
in general: §1.6.

| Game | What it proves | Starts after | Render pipeline |
|---|---|---|---|
| `GreyBox2D/` | The API works from outside the package, in a second genre | Phase 2 | not created yet |
| `GreyBoxStrategy/` | A faction agent with no Transform — an agent is not an NPC. **Never cut** | Phase 3 | not created yet |
| `CompanionRPG/` | A companion built with the Inspector tooling | Phase 5 | not created yet |

## Rules

- **One Unity project per game**, directly under `Demos/`.
- **Same Unity version as `UnityProject/`** — 6000.3.23f1. CI fails otherwise.
- **The framework comes from this repo by relative path**, never a version or git URL —
  those pin a copy, and the game stops testing the framework. CI fails otherwise.
- **Any render pipeline.** Pick whatever suits the game; the framework references none.
- **Game code goes in the game's own assembly definition**, referencing only the
  framework's public assemblies: `Agenerela`, plus `Agenerela.Editor` from editor-only
  code. No `InternalsVisibleTo`, no copied framework code. Needing either means the
  framework is missing a public API — fix it there.
- **Framework work happens in `UnityProject/`**, where the tests are. Create new framework
  files from there, so one editor generates their `.meta` files.

## Adding a game project

1. **Create it in Unity Hub.** New project, editor **6000.3.23f1**, and the template for
   the render pipeline you want (Universal 3D, Universal 2D, High Definition 3D, or one of
   the Built-In Render Pipeline templates). Location: this `Demos/` folder. Project name:
   the game's folder name, no spaces. If Hub offers Unity Version Control, leave it off —
   this repo uses git.
2. **Close the editor** once it has finished importing.
3. **Add the framework** to `Demos/<Game>/Packages/manifest.json`, inside `"dependencies"`:

   ```json
   "com.agenerela.framework": "file:../../../UnityProject/Packages/com.agenerela.framework",
   ```

   The path starts from the project's `Packages/` folder, hence three `..`.
4. **Reopen the project.** Package Manager → *In Project* should list
   *Agenerela — AI Agents for Unity*, and the console should be clean.
5. **Add the game's assembly definition** in `Assets/<Game>/`, named `<Game>`, with
   `Agenerela` under *Assembly Definition References*. All game code goes under it.
6. **Check nothing generated is staged.** This must not list `Library/`, `Temp/`,
   `Logs/`, `UserSettings/` or any `*.csproj`:

   ```bash
   git status --porcelain
   ```

7. **Commit** `Assets/`, `Packages/manifest.json`, `Packages/packages-lock.json` and
   `ProjectSettings/`, and fill in the game's row in the table above. CI finds the new
   project on its own.

## Working on a game

- **Open only the game you are working on.** Open `UnityProject/` for framework work.
- **Changed the framework's public API?** Open every game project, fix what breaks, and
  put the fixes in the same pull request. CI cannot compile Unity projects yet (build plan
  §6.3), so this check is manual for now.
- **To edit framework code from a game's IDE solution**, tick *Local packages* under
  *Preferences → External Tools → Generate .csproj files for*.
- **Opening a game after a framework change can update its `Packages/packages-lock.json`.**
  Commit that with the change.
- **Code two games share** goes in a local package at `Demos/Shared/com.agenerela.demos.<name>/`,
  with its own assembly definition and a `package.json` that has `name`, `version`,
  `displayName`, `description` and `unity` (CI checks all five). Each game references it
  the same way as the framework: `"file:../../Shared/com.agenerela.demos.<name>"`. Until a
  second game needs something, keep it in the game that has it.

## Git LFS — fetch only your game

Art, audio and models go through Git LFS (patterns in [`.gitattributes`](../.gitattributes)).
The free quota is small (build plan §6.4), and every teammate who pulls every game's art
counts against it. To skip the games you do not work on, list them in `lfs.fetchexclude`.
The setting is per clone, not committed. For someone working on `GreyBox2D`:

```bash
git config lfs.fetchexclude "Demos/CompanionRPG/**,Demos/GreyBoxStrategy/**"
```

Files in excluded folders are left as small pointer files on every later pull and
checkout. **Do not open an excluded game in Unity** — it would import the pointers as
broken assets. To get a game back, remove it from the list (or clear the setting) and
download what you skipped:

```bash
git config --unset lfs.fetchexclude
```

```bash
git lfs pull
```

To skip them from the very first clone, pass the setting to `git clone`:

```bash
git clone -c lfs.fetchexclude="Demos/CompanionRPG/**,Demos/GreyBoxStrategy/**" https://github.com/agenerela/agenerela.git
```
