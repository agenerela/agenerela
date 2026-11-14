using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GreyBoxStrategy.Replay;
using UnityEngine;

namespace GreyBoxStrategy.Realm
{
    /// <summary>
    /// The writs turn played on the map (rules v2, R1): armies march at the same moment, a battle clashes
    /// where it happens and its loser falls back, occupation is hatched in from the side the army came
    /// from, freed land's hatching lifts, and a treaty turns hatching into the new owner's wash under a seal.
    /// </summary>
    public sealed partial class ReplayDirector
    {
        /// <summary>A step's slide at 1×, as the design page times it.</summary>
        private const float StepSeconds = 0.6f;

        /// <summary>A game played here by stand-in rulers: its setup, so the dev menu can play it again with an edit.</summary>
        private (GameSetup setup, MapDefinition map, GameRules rules)? scripted;

        /// <summary>
        /// A dev edit in a writs game with stand-in rulers: the game is played again with the edit at the
        /// start of the next turn, so the replay shows what the rules make of it. The turns before stay as they were.
        /// </summary>
        private string WritsEdit(string command)
        {
            var s = scripted.Value;
            int now = game.Turns.Count == 0 ? 0 : game.Turns[Math.Min(turnIndex, game.Turns.Count - 1)].Turn;
            int at = now + 1;
            var edits = (sourceRecording?.DevEdits ?? new List<DevEdit>()).Where(e => e.Turn < at || e.Turn == at).ToList();
            edits.Add(new DevEdit { Turn = at, Command = command });
            try
            {
                var rec = ScriptedWorldGame.Play(s.setup, s.map, s.rules, edits);
                var rebuilt = ReplayBuilder.Build(rec, s.map, s.rules);
                sourceRecording = rec;
                game = rebuilt;
                queries = new RealmQueries(game, game.Map, game.Rules);
                ui.Queries = queries;
            }
            catch (Exception e) { return $"cannot apply at turn {at}: {e.GetBaseException().Message}"; }
            return $"applied at the start of turn {at}: the stand-in rulers play on from there";
        }

        /// <summary>
        /// For the menu, the dev menu and the Unity CLI: a live writs game on the Classic board, here and now.
        /// Model rulers on the RTX 5080 behind its lock (checked first: the environment, the lock, its pause
        /// file, the server), or <paramref name="scripted"/> stand-ins through the same agents, with no server.
        /// </summary>
        public void TryLiveWrits(string model, int seed, string victory = GameSetup.ConquestPreset, bool scripted = false, int turns = 0, string arm = null) =>
            StartCoroutine(StartLiveWrits(model, seed, victory, scripted, turns, arm));

        private IEnumerator StartLiveWrits(string model, int seed, string victory, bool scripted, int turns, string arm)
        {
            if (started || live == null) yield break;
            if (!scripted)
            {
                string why = LiveGame.WhyNotNow();
                if (why != null) { ui.ShowMenuStatus(why); yield break; }
                ui.ShowMenuStatus("Asking the server...", true);
                string serverWhy = null;
                yield return Gateway.CheckServer(w => serverWhy = w);
                if (serverWhy != null) { ui.ShowMenuStatus(serverWhy); yield break; }
            }
            started = true;
            var setup = GameSetup.Writs(seed, GameSetup.ClassicMapId, victory);
            var map = setup.BuildMap();
            var rules = RulesFor(setup, map);
            live.Configure(true, seed, turns, model);
            live.BeginWrits(map, rules, seed, scripted, arm);
            if (live.Error != null) { ui.ShowMenuStatus(live.Error); started = false; yield break; }
            ui.Players = live.Players;
            game = live.Replay();
            UseGame();
            ui.HideMenu();
            Show(game.Initial, null);
            StartCoroutine(Play());
        }

        /// <summary>The screen's links to the game on it, after the game changed under a screen already built.</summary>
        private void UseGame()
        {
            queries = new RealmQueries(game, game.Map, game.Rules);
            ui.Queries = queries;
            ui.WinAt = game.Rules?.ProvincesToWin ?? 15;
            ui.Conquest = game.Rules != null && game.Rules.Victory == VictoryMode.Conquest;
            if (ui.Conquest) ui.WinAt = game.ProvinceIds.Count;
            ui.MaxTurns = game.Rules?.MaxTurns ?? 30;
            WireWrits();
        }

        private void WireWrits()
        {
            ui.Writs = game.Writs;
            if (!game.Writs) return;
            ui.TurnsLeft = TurnsLeft;
            ui.HostPressed = id =>
            {
                var a = shown?.Army(id);
                if (a == null) return;
                // Rule mode: your army with a writ is chosen for orders; anything else opens where it stands.
                if (rule != null && rule.Writs && ui.OrdersRequest != null && a.Owner == rule.You && ui.ArmiesWithWrits(ui.OrdersRequest).Contains(id))
                {
                    if (ui.SelectedArmy != id) { ui.SelectedArmy = id; ApplyMode(); lastOrdersStatus = null; RuleTick(); }
                    return;
                }
                int at = game.IndexOf(a.Location);
                if (at >= 0) OpenProvince(at);
            };
        }

        /// <summary>The turns an army needs for the rest of its path: rough ground takes two, open ground two steps a turn.</summary>
        private int TurnsLeft(ArmyState a)
        {
            var rules = game.Rules ?? new GameRules();
            float turns = 0; int open = 0;
            for (int k = 0; k < a.Path.Count; k++)
            {
                var p = game.Map.Provinces.FirstOrDefault(x => x.Id == a.Path[k]);
                int cost = p == null ? 1 : rules.TurnsToEnter(p.Terrain);
                if (k == 0 && a.StepTurnsLeft > 0) cost = a.StepTurnsLeft;
                if (cost == 1 && open > 0 && open < rules.MarchStepsPerTurn) { open++; continue; }
                turns += cost; open = cost == 1 ? 1 : 0;
            }
            return Math.Max(1, (int)Math.Ceiling(turns));
        }

        private void ShowOccupiers(ReplayState s)
        {
            if (game == null || !game.Writs) return;
            map.SetOccupiers(s.Occupiers, id => CountryLook.Of(id).Colour);
        }

        private int Index(string province) => game.IndexOf(province);

        private static string Cap(string id) => string.IsNullOrEmpty(id) ? "" : char.ToUpperInvariant(id[0]) + id.Substring(1);

        private static bool WritsKind(ReplayEventKind k) =>
            k == ReplayEventKind.ArmyOrder || k == ReplayEventKind.March || k == ReplayEventKind.Battle || k == ReplayEventKind.Occupy
            || k == ReplayEventKind.Free || k == ReplayEventKind.TakeNeutral || k == ReplayEventKind.Treaty || k == ReplayEventKind.ArmyLost
            || k == ReplayEventKind.Broken || k == ReplayEventKind.Stalemate;

        private IEnumerator LandWrits(ReplayEvent e, ChronicleLine line)
        {
            switch (e.Kind)
            {
                case ReplayEventKind.ArmyOrder:
                {
                    // Many a turn: quick, and spoken only when the marshal said something.
                    var banner = string.IsNullOrWhiteSpace(e.Statement) ? null : ui.Say(e.Country, e.Statement, Index(shown.Army(e.Army)?.Location), Why(e));
                    Show(e.After, null);
                    AddLine(e, line);
                    yield return Wait(banner == null ? 0.12f : Mathf.Max(0.6f, Reading(e.Statement) * 0.6f));
                    ui.Unsay(banner);
                    break;
                }
                case ReplayEventKind.March:
                    yield return March(e);
                    break;
                case ReplayEventKind.Battle:
                    yield return Battle(e, line);
                    break;
                case ReplayEventKind.Occupy:
                {
                    int p = e.ProvinceIndex;
                    int from = NearestHeldBy(e.Country, p, shown);
                    for (float t = 0; t < 1; t += Time.deltaTime * speed / 0.8f) { map.SetHatch(p, e.Country, id => CountryLook.Of(id).Colour, t, from); yield return null; }
                    Show(e.After, null);
                    AddLine(e, line);
                    yield return Wait(0.5f);
                    break;
                }
                case ReplayEventKind.Free:
                {
                    int p = e.ProvinceIndex;
                    for (float t = 0; t < 1; t += Time.deltaTime * speed / 0.6f) { map.SetHatch(p, e.Other, id => CountryLook.Of(id).Colour, 1 - t); yield return null; }
                    Show(e.After, null);
                    AddLine(e, line);
                    yield return Wait(0.5f);
                    break;
                }
                case ReplayEventKind.TakeNeutral:
                    yield return Capture(e, line);
                    break;
                case ReplayEventKind.Treaty:
                    yield return Treaty(e, line);
                    break;
                case ReplayEventKind.ArmyLost:
                case ReplayEventKind.Broken:
                case ReplayEventKind.Stalemate:
                    Show(e.After, null);
                    AddLine(e, line);
                    yield return Wait(e.Kind == ReplayEventKind.ArmyLost ? 0.5f : 1.4f);
                    break;
            }
        }

        // Every army steps at once, a province at a time; one waiting on rough ground slides to the border.
        private IEnumerator March(ReplayEvent e)
        {
            int steps = e.Routes.Max(r => r.Steps.Count + (r.Towards != null ? 1 : 0));
            for (int k = 0; k < steps; k++)
            {
                for (float t = 0; t < 1; t += Time.deltaTime * speed / StepSeconds)
                {
                    float s = Smooth(t);
                    foreach (var r in e.Routes) MoveAlong(r, k, s);
                    yield return null;
                }
                foreach (var r in e.Routes) MoveAlong(r, k, 1);
            }
            Show(e.After, null);
            ui.StopHostMotion();
            yield return Wait(0.15f);
        }

        private void MoveAlong(ArmyRoute r, int k, float t)
        {
            string last = r.Steps.Count > 0 ? r.Steps[r.Steps.Count - 1] : r.From;
            if (k < r.Steps.Count) ui.MoveHost(r.Army, Index(k == 0 ? r.From : r.Steps[k - 1]), Index(r.Steps[k]), t);
            else if (k == r.Steps.Count && r.Towards != null) ui.MoveHost(r.Army, Index(last), Index(r.Towards), 0.45f * t);
            else if (k == 0 || r.Steps.Count > 0) ui.MoveHost(r.Army, Index(last), Index(r.Towards ?? last), r.Towards != null ? 0.45f : 1);
        }

        // The clash where it happened, a puff, the result; the loser slides back to where it falls back to.
        private IEnumerator Battle(ReplayEvent e, ChronicleLine line)
        {
            var b = e.Battle;
            int p = b.Province != null ? Index(b.Province) : Index(b.Border?[0]);
            int q = b.Province != null ? -1 : Index(b.Border?[1]);
            map.SetFocus(p, 0);
            var clash = ui.ShowClash(p, q);
            for (float t = 0; t < 1; t += Time.deltaTime * speed / 0.35f) { ui.SetClash(clash, Smooth(t), 0); map.SetFocus(p, Smooth(t) * 0.7f); yield return null; }
            for (float t = 0; t < 1; t += Time.deltaTime * speed / 0.55f) { ui.SetClash(clash, 1, t); yield return null; }
            ui.SetClash(clash, 1, 0);
            // The losers fall back.
            var from = b.Province ?? b.Border?[b.AttackerWon ? 1 : 0];
            var moving = b.Retreats.Where(kv => Index(kv.Value) >= 0).ToList();
            if (moving.Count > 0)
            {
                for (float t = 0; t < 1; t += Time.deltaTime * speed / 0.45f)
                {
                    foreach (var kv in moving) ui.MoveHost(kv.Key, Index(shown.Army(kv.Key)?.Location ?? from), Index(kv.Value), Smooth(t));
                    yield return null;
                }
            }
            Show(e.After, null);
            ui.StopHostMotion();
            ui.AddLine(line);
            ui.ShowNotice(null);
            yield return Wait(0.5f);
            ui.HideClash(clash);
            map.SetFocus(-1, 0);
        }

        // The treaty's moment (v2 5.1): ceded land's hatching thickens and flows into the new owner's wash
        // under a wax seal; returned land's hatching lifts.
        private IEnumerator Treaty(ReplayEvent e, ChronicleLine line)
        {
            var banner = ui.Say(e.Country, e.Statement, e.ProvinceIndex, Why(e));
            yield return Wait(0.4f);
            string gains = e.Term == Actions.CedeOccupied ? e.Other : e.Country;
            Func<string, Color32> colour = id => CountryLook.Of(id).Colour;
            var ceded = e.Ceded.Select(Index).Where(i => i >= 0).ToList();
            var returned = e.Returned.Select(Index).Where(i => i >= 0).ToList();
            var occupier = Enumerable.Range(0, shown.Occupiers.Length).ToDictionary(i => i, i => shown.Occupiers[i]);
            for (float t = 0; t < 1; t += Time.deltaTime * speed / 0.5f)
            {
                foreach (int p in ceded) map.SetHatch(p, occupier[p], colour, 1, -1, Smooth(t));
                foreach (int p in returned) map.SetHatch(p, occupier[p], colour, 1 - Smooth(t));
                yield return null;
            }
            foreach (int p in ceded)
            {
                map.SetHatch(p, null, colour, 0);
                map.BeginCapture(p, gains, shown.Owners[p], colour, id => CountryLook.Of(id).Slot, shown.Owners);
            }
            for (float t = 0; t < 1; t += Time.deltaTime * speed / 1.0f)
            {
                foreach (int p in ceded) map.SetCaptureProgress(p, 1, 1 - Mathf.Pow(1 - t, 3));
                yield return null;
            }
            foreach (int p in ceded) { map.EndCapture(p); ui.StampSeal(p, gains); }
            Show(e.After, gains);
            ui.AddLine(line);
            yield return Wait(Mathf.Max(1.0f, Reading(e.Statement) - 1.5f));
            ui.Unsay(banner);
        }

        /// <summary>The province held by <paramref name="country"/> nearest to <paramref name="p"/>: where an occupation is drawn in from.</summary>
        private int NearestHeldBy(string country, int p, ReplayState s)
        {
            var held = Enumerable.Range(0, game.ProvinceIds.Count).Where(q => q != p && s.Controller(q) == country).ToList();
            if (held.Count == 0) return -1;
            return held.OrderBy(q => (map.ProvinceWorld(q) - map.ProvinceWorld(p)).sqrMagnitude).First();
        }
    }
}
