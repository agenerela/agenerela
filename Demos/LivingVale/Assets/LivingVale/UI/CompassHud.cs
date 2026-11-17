using System.Collections.Generic;
using LivingVale.Game;
using LivingVale.Unity;
using LivingVale.World;
using UnityEngine;
using UnityEngine.UI;

namespace LivingVale.UI
{
    /// <summary>
    /// G8a: the compass strip at the top centre. It shows 180° around where the camera looks, the
    /// eight compass points, every discovered place by name, and the current quest goal as a gold
    /// marker, with a readout under the strip naming the goal and its distance. A goal behind the
    /// player clamps to the strip's edge with an arrow, so the player knows which way to turn.
    /// </summary>
    [DefaultExecutionOrder(400)]
    public sealed class CompassHud : MonoBehaviour
    {
        private const float Span = 180f;
        private static readonly string[] Points = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
        private static readonly Color Gold = new Color(1f, 0.82f, 0.3f);

        private ValeBootstrap bootstrap;
        private Transform player;
        private NavigationTargets targets;
        private Font font;
        private RectTransform strip;
        private readonly List<Text> points = new List<Text>();
        private readonly List<(ValePlace place, Text label)> places = new List<(ValePlace, Text)>();
        private RectTransform goalMarker;
        private Text goalReadout;

        public void Initialize(ValeBootstrap owner, Transform playerTransform, NavigationTargets navigation)
        {
            bootstrap = owner;
            player = playerTransform;
            targets = navigation;
            Build();
        }

        private void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -10;
            HudLayout.Scale(gameObject);
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            strip = Rect("Compass strip", transform, new Vector2(0.5f, 1f), new Vector2(0f, -HudLayout.CompassTop),
                new Vector2(HudLayout.CompassWidth, HudLayout.CompassHeight));
            var back = strip.gameObject.AddComponent<Image>();
            back.color = new Color(0.04f, 0.06f, 0.08f, 0.78f);
            back.raycastTarget = false;
            strip.gameObject.AddComponent<RectMask2D>();

            RectTransform tick = Rect("Centre tick", strip, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(2f, 6f));
            Image tickImage = tick.gameObject.AddComponent<Image>();
            tickImage.color = new Color(1f, 1f, 1f, 0.8f);
            tickImage.raycastTarget = false;

            foreach (string point in Points)
            {
                Text label = Label(point, strip, 14, point.Length == 1 ? Color.white : new Color(0.75f, 0.8f, 0.85f));
                label.rectTransform.anchoredPosition = new Vector2(0f, 5f);
                points.Add(label);
            }

            foreach (ValePlace place in ValePlaces.All)
            {
                Text label = Label(place.DisplayName, strip, 11, new Color(0.7f, 0.9f, 0.7f));
                label.rectTransform.sizeDelta = new Vector2(110f, 14f);
                label.rectTransform.anchoredPosition = new Vector2(0f, -7f);
                places.Add((place, label));
            }

            goalMarker = Rect("Goal marker", strip, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(9f, 9f));
            goalMarker.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Image goalImage = goalMarker.gameObject.AddComponent<Image>();
            goalImage.color = Gold;
            goalImage.raycastTarget = false;

            goalReadout = Label("Goal readout", transform, 15, Gold);
            RectTransform readout = goalReadout.rectTransform;
            readout.anchorMin = readout.anchorMax = readout.pivot = new Vector2(0.5f, 1f);
            readout.anchoredPosition = new Vector2(0f, -HudLayout.GoalTop);
            readout.sizeDelta = new Vector2(HudLayout.CompassWidth, 20f);
        }

        private void LateUpdate()
        {
            if (strip == null || player == null)
            {
                return;
            }

            Transform view = Camera.main != null ? Camera.main.transform : player;
            Vector3 forward = Vector3.ProjectOnPlane(view.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = player.forward;
            }

            float heading = NavigationMath.Heading(forward.x, forward.z);
            float width = HudLayout.CompassWidth;
            for (int i = 0; i < points.Count; i++)
            {
                Place(points[i], i * 45f, heading, width, 5f);
            }

            Vector3 feet = player.position;
            foreach (var (place, label) in places)
            {
                bool known = bootstrap.Discovery.IsDiscovered(place.Id) && bootstrap.Discovery.Current != place;
                label.enabled = known;
                if (known)
                {
                    Place(label, NavigationMath.Bearing(feet.x, feet.z, place.X, place.Z), heading, width, -7f);
                }
            }

            bool chatting = bootstrap.Coordinator.IsOpen;
            QuestGoal goal = targets.Goal;
            bool located = targets.TryLocate(goal, out Vector3 target);
            goalMarker.gameObject.SetActive(located);
            goalReadout.enabled = located && !chatting;
            if (!located)
            {
                return;
            }

            float bearing = NavigationMath.Bearing(feet.x, feet.z, target.x, target.z);
            bool onStrip = NavigationMath.StripOffset(bearing, heading, Span, width, out float x);
            float edge = width * 0.5f - 6f;
            goalMarker.anchoredPosition = new Vector2(Mathf.Clamp(x, -edge, edge), 0f);
            float metres = Vector3.ProjectOnPlane(target - feet, Vector3.up).magnitude;
            string text = $"{goal.Label} · {NavigationMath.Distance(metres)}";
            if (!onStrip || Mathf.Abs(x) > edge)
            {
                text = x < 0f ? "< " + text : text + " >";
            }

            goalReadout.text = text;
        }

        private static void Place(Text label, float bearing, float heading, float width, float y)
        {
            // A little past the edge still draws, so a label slides out under the mask instead of popping.
            NavigationMath.StripOffset(bearing, heading, Span, width, out float x);
            bool shown = Mathf.Abs(x) <= width * 0.5f + 30f;
            label.enabled = shown;
            if (shown)
            {
                label.rectTransform.anchoredPosition = new Vector2(x, y);
            }
        }

        private Text Label(string text, Transform parent, int size, Color colour)
        {
            RectTransform rect = Rect(text, parent, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40f, 18f));
            var label = rect.gameObject.AddComponent<Text>();
            label.font = font;
            label.fontSize = size;
            label.color = colour;
            label.text = text;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.supportRichText = false;
            label.raycastTarget = false;
            rect.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.7f);
            return label;
        }

        private static RectTransform Rect(string objectName, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(objectName, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }
    }
}
