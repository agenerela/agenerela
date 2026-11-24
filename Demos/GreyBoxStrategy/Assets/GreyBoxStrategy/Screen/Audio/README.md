# Painted Realm's sounds

Every sound the game plays is a cue in one table, `Screen/Replay/SoundCues.cs`: its name, its bus (music, effects,
interface, voice), its file under `Resources/Sounds/`, its level and where it plays. To change a sound, replace its
file or change its line in the table; no code that plays it changes. `SoundManager.Play("<name>")` plays one.
Settings → Sound test plays each cue at the levels set above it.

## How these were made

On the team's asset server (`tools/comfyui/`, the `sfx` workflow: Stable Audio 3 Medium), on 17 November 2026, at
`quality=320k`, three candidates a cue under the RTX 5080's lock (held 51 s for the batch). Remake one with:

```bash
python tools/comfyui/comfy.py run sfx --set "prompt=<the prompt>" --set seconds=<seconds> --set seed=<seed> --set quality=320k
```

Each pick was then trimmed of its silence (to -48 dB below its peak, with 2 ms and up to 40 ms fades), normalised to a
-3 dBFS peak and encoded to Ogg Vorbis at quality 5 (`.scratch/realm-menu-sound/prepare.py` on the machine that made
them). Every candidate is kept in `.scratch/realm-menu-sound/candidates/` there.

| Cue | Seconds asked | Seed | Prompt |
|---|---|---|---|
| `ui_click` | 1 | 519277320026686 | A single soft dry click of a small wooden latch tapped on a parchment-covered writing board, very close, tiny quiet room, no reverb tail. Length: 1 second |
| `ui_hover` | 1 | 423286856644917 | A very soft brief brush of a fingertip across thick dry parchment, barely audible, close and dry, no tone. Length: 1 second |
| `panel_open` | 2 | 1065909566895986 | A heavy parchment map unrolled across an oak table, a short papery rustle ending in a soft muffled thump, close, dry study room. Length: 2 seconds |
| `panel_close` | 1.5 | 279046171746702 | A sheet of thick parchment folded once and laid down on a wooden desk, a short soft papery swish, close and dry. Length: 1.5 seconds |
| `end_turn` | 3 | 45523478228450 | A wax seal pressed firmly onto a folded letter on a wooden desk, a soft squash and a dull wooden knock, then one distant low bronze bell strike slowly fading. Length: 3 seconds |
| `turn_resolves` | 2.5 | 101968631006193 | A quill pen scratching a short line onto parchment, then the page turned over with a soft papery rustle, quiet stone study room. Length: 2.5 seconds |

## How each was chosen, without listening

The agent that picked them cannot hear. Each candidate was measured (length, the sound's active part, leading
silence, peak and RMS level, clipped samples, spectral centroid and flatness) and drawn as a waveform over a
spectrogram, and picked on:

- **No clipping** and the most headroom of the three.
- **Starts at once.** A click with a tenth of a second of silence before it feels late; picks were trimmed to their onset.
- **The right shape.** One transient for a click; a short broadband rustle for a hover or a page; for the end of a
  turn, partials that ring and fade (a struck bell or chime).
- **No voice and no melody.** None showed formants or a run of changing pitches; the only tonal one is `end_turn`'s
  single strike.

What the measures cannot tell is whether a sound is pleasant. **These are drafts until the lead hears them.** The
first to replace is `end_turn`: all three came out as one bright chime (centroid 4 to 6 kHz) rather than a seal and a
low bell.
