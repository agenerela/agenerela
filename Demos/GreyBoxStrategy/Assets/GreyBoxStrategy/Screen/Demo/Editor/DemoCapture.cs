using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.Media;
using UnityEngine;

namespace GreyBoxStrategy.Realm.Demo.Editor
{
    /// <summary>
    /// Records a demo timeline to an MP4 (H.264), from the Unity CLI, with the editor in the background. Demo only; the
    /// game never needs it.
    /// <code>
    /// unity command script eval -- --code 'return GreyBoxStrategy.Realm.Demo.Editor.DemoCapture.Prepare(3840, 2160);'
    /// unity command editor.playmode start
    /// unity command script eval -- --code 'return GreyBoxStrategy.Realm.Demo.Editor.DemoCapture.Begin("sample", 3840, 2160, 60);'
    /// </code>
    /// Progress is in <c>Logs/Demo/status.txt</c>, and the file is <c>Logs/Demo/&lt;timeline&gt;-&lt;w&gt;x&lt;h&gt;.mp4</c>, both in
    /// the project, which git ignores.
    /// <para>
    /// The game's clock is the frame count (<see cref="Time.captureFramerate"/>), so the file holds its frame rate however
    /// slowly frames render, and every take is the same. Each frame is grabbed from the Game view at the frame's end
    /// and read back at once. Unity Recorder was tried first (24 November): its read-back is asynchronous and completes
    /// only when the editor presents a frame, which an editor in the background does about once a second, so it
    /// recorded at one frame a second. This uses Unity's own <see cref="MediaEncoder"/>, the H.264 encoder Recorder wraps.
    /// </para>
    /// </summary>
    public static class DemoCapture
    {
        private static MediaEncoder encoder;
        private static RenderTexture grab, upright;
        private static Texture2D frame;
        private static Grabber grabber;
        private static string output;
        private static int width, height, fps, frames;
        private static float megabits;
        private static DateTime began;

        /// <summary>"idle", "setting the scene", "recording …", "done: …", or what went wrong.</summary>
        public static string Status { get; private set; } = "idle";

        /// <summary>In edit mode, before Play: the Game view renders at the capture's size, so the screen lays out for it.</summary>
        public static string Prepare(int w, int h)
        {
            PlayModeWindow.SetCustomRenderingResolution((uint)w, (uint)h, $"demo {w}x{h}");
            return $"Game view {w} × {h}";
        }

        /// <summary>
        /// In Play mode, on the start menu: starts the timeline, records from its <c>roll</c> step to its end.
        /// <paramref name="mbps"/> is the H.264 bit rate; 0 picks one for the size (about 0.2 bits a pixel a frame).
        /// </summary>
        public static string Begin(string timeline, int w, int h, int framesPerSecond, float mbps = 0)
        {
            if (!EditorApplication.isPlaying) return "Enter Play mode first (editor.playmode start).";
            if (encoder != null) return "Already recording: " + Status;
            width = w; height = h; fps = framesPerSecond;
            megabits = mbps > 0 ? mbps : Mathf.Max(12, w * h * fps * 0.2f / 1e6f);
            string folder = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Logs", "Demo");
            Directory.CreateDirectory(folder);
            output = Path.Combine(folder, $"{Path.GetFileNameWithoutExtension(timeline)}-{w}x{h}.mp4").Replace('\\', '/');

            DemoDirector.Rolling -= OnRolling; DemoDirector.Rolling += OnRolling;
            DemoDirector.Finished -= OnFinished; DemoDirector.Finished += OnFinished;
            Status = "setting the scene";
            string started = DevTools.Run("demo " + timeline);
            if (!started.StartsWith("playing", StringComparison.Ordinal))
            {
                DemoDirector.Rolling -= OnRolling; DemoDirector.Finished -= OnFinished;
                Status = started;
                return started;
            }
            return $"{started}; recording from its roll step to {output} at {fps} fps, {megabits:0} Mbit/s";
        }

        /// <summary>The timeline's roll step: the scene is set, the clock is held to the frame rate, frames are taken from now.</summary>
        private static void OnRolling()
        {
            DemoDirector.Rolling -= OnRolling;
            if (Screen.width != width || Screen.height != height)
                Debug.LogWarning($"[demo] the Game view is {Screen.width} × {Screen.height}, not {width} × {height}: run Prepare before Play");
            var h264 = new H264EncoderAttributes { gopSize = (uint)fps, numConsecutiveBFrames = 2, profile = VideoEncodingProfile.H264High };
            var attributes = new VideoTrackEncoderAttributes(h264)
            {
                frameRate = new MediaRational(fps),
                width = (uint)width,
                height = (uint)height,
                includeAlpha = false,
                bitRateMode = VideoBitrateMode.High,
                targetBitRate = (uint)(megabits * 1e6f),
            };
            if (File.Exists(output)) File.Delete(output);
            encoder = new MediaEncoder(output, attributes);
            grab = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
            upright = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
            frame = new Texture2D(width, height, TextureFormat.RGBA32, false);
            frames = 0;
            began = DateTime.UtcNow;
            Time.captureFramerate = fps;
            grabber = new GameObject("demo-capture") { hideFlags = HideFlags.HideAndDontSave }.AddComponent<Grabber>();
            Status = $"recording to {output}";
            WriteStatus();
        }

        /// <summary>One frame, at the end of the frame: the Game view, turned upright, read back now, encoded.</summary>
        private static void Grab()
        {
            if (encoder == null) return;
            ScreenCapture.CaptureScreenshotIntoRenderTexture(grab);
            // The capture comes upside down where texture rows start at the top (Direct3D, Metal, Vulkan).
            if (SystemInfo.graphicsUVStartsAtTop) Graphics.Blit(grab, upright, new Vector2(1, -1), new Vector2(0, 1));
            else Graphics.Blit(grab, upright);
            var was = RenderTexture.active;
            RenderTexture.active = upright;
            frame.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
            frame.Apply(false);
            RenderTexture.active = was;
            encoder.AddFrame(frame);
            frames++;
            var d = DemoDirector.Active;
            Status = $"recording to {output}: {frames} frames ({frames / (float)fps:0.0} s), {(DateTime.UtcNow - began).TotalSeconds:0} s real, {(d != null ? d.Status : "no demo")}";
            if (frames % 30 == 0) WriteStatus();
        }

        private static void OnFinished(string error)
        {
            DemoDirector.Finished -= OnFinished;
            DemoDirector.Rolling -= OnRolling;
            // A few frames more, so the last fade's black is in the file.
            int more = 6;
            void Stop()
            {
                if (more-- > 0 && EditorApplication.isPlaying) return;
                EditorApplication.update -= Stop;
                Close();
                Status = error != null ? $"stopped, the demo failed: {error}; partial file {output}"
                    : $"done: {output}, {frames} frames, {frames / (float)fps:0.0} s, {(DateTime.UtcNow - began).TotalSeconds:0} s to render";
                Debug.Log("[demo] " + Status);
                WriteStatus();
            }
            EditorApplication.update += Stop;
        }

        /// <summary>Ends the file and lets go of everything; safe to call twice. Leaving Play mode calls it too.</summary>
        public static string Close()
        {
            Time.captureFramerate = 0;
            if (grabber != null) UnityEngine.Object.Destroy(grabber.gameObject);
            grabber = null;
            encoder?.Dispose();
            encoder = null;
            if (grab != null) { grab.Release(); UnityEngine.Object.Destroy(grab); grab = null; }
            if (upright != null) { upright.Release(); UnityEngine.Object.Destroy(upright); upright = null; }
            if (frame != null) { UnityEngine.Object.Destroy(frame); frame = null; }
            return "closed";
        }

        [InitializeOnLoadMethod]
        private static void CloseOnExit() => EditorApplication.playModeStateChanged += s =>
        {
            if (s != PlayModeStateChange.ExitingPlayMode || encoder == null) return;
            Close();
            Status = "stopped: Play mode ended";
            WriteStatus();
        };

        /// <summary>The status, also in <c>Logs/Demo/status.txt</c>, so a session can watch it without asking the editor.</summary>
        private static void WriteStatus()
        {
            if (output == null) return;
            try { File.WriteAllText(Path.Combine(Path.GetDirectoryName(output), "status.txt"), Status + Environment.NewLine); }
            catch (IOException) { }
        }

        /// <summary>Takes each frame once everything, the UI included, has drawn.</summary>
        private sealed class Grabber : MonoBehaviour
        {
            private void LateUpdate() => StartCoroutine(AtEnd());

            private static IEnumerator AtEnd()
            {
                yield return new WaitForEndOfFrame();
                Grab();
            }
        }
    }
}
