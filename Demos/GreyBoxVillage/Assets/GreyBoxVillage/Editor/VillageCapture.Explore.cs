using System.Collections;
using System.Collections.Generic;
using System.IO;
using Demos.Shared;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Demos.GreyBoxVillage.Editor
{
    /// <summary>
    /// Slice 3's views, in Play mode so everyone walks: the villagers' routine at four hours and the
    /// village in each weather. <c>-executeMethod
    /// Demos.GreyBoxVillage.Editor.VillageCapture.CaptureExploreFromCommandLine -captureOut DIR</c>, with
    /// no <c>-quit</c> and no <c>-nographics</c>.
    /// </summary>
    public static partial class VillageCapture
    {
        public static void CaptureExploreFromCommandLine() =>
            Run(ExploreShots(Arg("-captureOut") ?? Path.GetFullPath("Logs/captures")), true);

        private static readonly (string name, Vector3 from, Vector3 to)[] RoutineViews =
        {
            ("square", new Vector3(-11, 9, -14), new Vector3(8, 0.5f, 2)),
            ("fields", new Vector3(-16, 8, -33), new Vector3(1, 0.5f, -21)),
            ("campfire", new Vector3(5, 5, -1), new Vector3(14, 0.5f, -10)),
        };

        private static IEnumerator ExploreShots(string folder)
        {
            Directory.CreateDirectory(folder);
            Application.targetFrameRate = 30;
            var commands = Object.FindFirstObjectByType<VillageCommands>();
            var config = ScriptableObject.CreateInstance<CaptureProviderConfig>();
            commands.UseProvider(config);
            // Nobody is asked anything: these are views of the village, not of decisions.
            foreach (var mind in commands.Minds)
                if (mind.Senses != null) mind.Senses.enabled = false;
            var events = commands.GetComponent<VillageEvents>();
            if (events != null) events.enabled = false;
            var panel = Resources.Load<PanelSettings>("VillagePanelSettings");
            var camera = Camera.main;
            var rig = camera.GetComponent<DemoCameraRig>();
            var saved = (panel.targetTexture, panel.clearColor, panel.colorClearValue);
            var (world, ui) = Targets(camera, panel, 1600, 900);
            try
            {
                if (rig != null) rig.enabled = false;
                // The player out of everyone's earshot, so no dialogue panel opens over the views.
                foreach (var x in Place(commands, new Vector3(-36f, 0, 12f), new Vector3(-36f, 0, 20f))) yield return x;
                commands.Day.Running = false;
                commands.Weather.Changes = false;

                // The routine: the morning's work, the square at midday, the campfire at dusk, home at night.
                foreach (var (hour, views) in new[]
                         {
                             (9f, new[] { "square", "fields" }), (12.5f, new[] { "square" }),
                             (18.6f, new[] { "campfire" }), (22.5f, new[] { "square" })
                         })
                {
                    commands.Day.TimeOfDay = hour / 24f;
                    commands.Day.Apply();
                    commands.Routine.CatchUp();
                    // Let them settle at their points, and the lamps and door lights come on.
                    foreach (var x in Frames(90)) yield return x;
                    foreach (var view in views)
                    {
                        var v = System.Array.Find(RoutineViews, r => r.name == view);
                        camera.transform.position = v.from;
                        camera.transform.LookAt(v.to);
                        Debug.Log("[VillageCapture] " + commands.Routine.Describe());
                        foreach (var x in Shoot(folder, "routine_" + hour.ToString("00.0", System.Globalization.CultureInfo.InvariantCulture) + "_" + view, world, ui))
                            yield return x;
                    }
                }

                // The weather, late morning, over the square and down the road outside the gate.
                commands.Day.TimeOfDay = 10.5f / 24f;
                commands.Day.Apply();
                commands.Routine.CatchUp();
                foreach (var kind in new[] { WeatherKind.Clear, WeatherKind.Overcast, WeatherKind.Rain, WeatherKind.Fog })
                {
                    commands.Weather.Set(kind);
                    commands.Weather.Snap();
                    foreach (var (name, from, to) in new[]
                             {
                                 ("square", new Vector3(-11, 9, -14), new Vector3(8, 0.5f, 2)),
                                 ("road", new Vector3(1.2f, 2.4f, -21f), new Vector3(0, 1.2f, -48f)),
                             })
                    {
                        camera.transform.position = from;
                        camera.transform.LookAt(to);
                        // The rain and the mist need a moment to fill the air round the camera.
                        foreach (var x in Frames(75)) yield return x;
                        foreach (var x in Shoot(folder, "weather_" + kind.ToString().ToLowerInvariant() + "_" + name, world, ui)) yield return x;
                    }
                }
                commands.Weather.Set(WeatherKind.Clear);
                commands.Weather.Snap();
            }
            finally
            {
                camera.targetTexture = null;
                Release(world, ui, panel);
                (panel.targetTexture, panel.clearColor, panel.colorClearValue) = saved;
                if (rig != null) rig.enabled = true;
                Application.targetFrameRate = -1;
                Object.DestroyImmediate(config);
            }
        }
    }
}
