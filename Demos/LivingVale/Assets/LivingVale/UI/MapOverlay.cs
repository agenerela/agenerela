using LivingVale.Game;
using LivingVale.Unity;
using LivingVale.World;
using UnityEngine;
using UnityEngine.UI;

namespace LivingVale.UI
{
    /// <summary>
    /// G8a: a north-up valley map with discovered places, known NPCs and the current quest goal.
    /// Built from code without raycast targets; its owner handles the keys that open and close it.
    /// </summary>
    [DefaultExecutionOrder(500)]
    public sealed class MapOverlay : MonoBehaviour
    {
        private const float MapSize = 480f;
        private static readonly Color Ink = new Color(0.95f, 0.93f, 0.85f);
        private static readonly Color Dim = new Color(0.36f, 0.4f, 0.36f);
        private static readonly Color Gold = new Color(1f, 0.82f, 0.3f);
        private static readonly Color NpcColour = new Color(0.55f, 0.8f, 0.85f);

        private sealed class Marker
        {
            public RectTransform Root;
            public Image Dot;
            public Text Name;
        }

        private ValeBootstrap bootstrap;
        private Transform player;
        private NavigationTargets targets;
        private Font font;
        private RectTransform root;
        private RectTransform map;
        private Marker[] places;
        private Marker[] npcs;
        private Marker goalMarker;
        private Text playerArrow;
        private Text instruction;

        // Label rectangles already drawn this frame, in map units, so crowded labels (the Glen holds
        // three NPCs, the goal and the player within a few pixels) skip rather than pile up.
        private readonly System.Collections.Generic.List<Rect> occupied = new System.Collections.Generic.List<Rect>();

        public bool IsOpen => root != null && root.gameObject.activeSelf;

        public void Initialize(ValeBootstrap owner, Transform player, NavigationTargets targets)
        {
            if (root != null)
            {
                return;
            }

            bootstrap = owner;
            this.player = player;
            this.targets = targets;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            root = Rect("Map overlay", transform, Vector2.zero, Vector2.zero);
            var canvas = root.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 20;
            var scaler = root.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform backdrop = Rect("Backdrop", root, Vector2.zero, Vector2.zero);
            backdrop.anchorMax = Vector2.one;
            Paint(backdrop, new Color(0f, 0f, 0f, 0.55f));
            RectTransform panel = Rect("Map panel", root, Vector2.zero, new Vector2(560f, 560f));
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
            Paint(panel, new Color(0.05f, 0.07f, 0.06f, 0.96f));
            Text title = Label("Title", panel, new Vector2(20f, 526f), new Vector2(520f, 28f), 22, Ink);
            title.text = "Map";
            title.alignment = TextAnchor.MiddleCenter;
            map = Rect("Map area", panel, new Vector2(40f, 40f), new Vector2(MapSize, MapSize));

            BuildPlaces();
            npcs = new Marker[targets.Npcs.Count];
            for (int i = 0; i < npcs.Length; i++)
            {
                npcs[i] = BuildMarker("NPC", 6f, 12, NpcColour);
                npcs[i].Root.gameObject.SetActive(false);
            }

            goalMarker = BuildMarker("Quest goal", 12f, 14, Gold);
            // The ASCII symbol agrees with the legend and needs no sprite asset.
            goalMarker.Dot.enabled = false;
            Text star = Label("Goal star", goalMarker.Root, Vector2.zero, new Vector2(22f, 22f), 24, Gold);
            star.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            star.alignment = TextAnchor.MiddleCenter;
            star.text = "*";
            goalMarker.Root.gameObject.SetActive(false);
            playerArrow = Label("Player", map, Vector2.zero, new Vector2(24f, 24f), 24, Ink);
            playerArrow.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            playerArrow.alignment = TextAnchor.MiddleCenter;
            playerArrow.text = "^";

            instruction = Label("Goal instruction", panel, new Vector2(20f, 22f),
                new Vector2(520f, 18f), 13, Gold);
            instruction.alignment = TextAnchor.MiddleCenter;
            instruction.horizontalOverflow = HorizontalWrapMode.Wrap;
            instruction.verticalOverflow = VerticalWrapMode.Truncate;
            Text legend = Label("Legend", panel, new Vector2(20f, 4f), new Vector2(520f, 18f), 13, Ink);
            legend.alignment = TextAnchor.MiddleCenter;
            legend.text = "M or Esc closes | * goal | ^ you";
            Hide();
        }

        public void Toggle()
        {
            if (root == null)
            {
                return;
            }

            if (IsOpen)
            {
                Hide();
                return;
            }

            root.gameObject.SetActive(true);
            RefreshPlaces();
        }

        public void Hide()
        {
            if (root != null)
            {
                root.gameObject.SetActive(false);
            }
        }

        private void LateUpdate()
        {
            if (IsOpen)
            {
                RefreshPlaces();
                RefreshDynamic();
            }
        }

        private void BuildPlaces()
        {
            foreach (var path in ValePlaces.Paths)
            {
                ValePlace from = ValePlaces.Get(path.from), to = ValePlaces.Get(path.to);
                Vector2 start = Point(from.X, from.Z), end = Point(to.X, to.Z);
                Vector2 delta = end - start;
                RectTransform line = Rect("Path", map, (start + end) * 0.5f, new Vector2(delta.magnitude, 3f));
                line.pivot = new Vector2(0.5f, 0.5f);
                line.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
                Paint(line, new Color(0.24f, 0.3f, 0.23f, 0.8f));
            }

            places = new Marker[ValePlaces.All.Count];
            for (int i = 0; i < places.Length; i++)
            {
                ValePlace place = ValePlaces.All[i];
                places[i] = BuildMarker("Place", 12f, 14, Ink);
                places[i].Root.anchoredPosition = Point(place.X, place.Z);
            }
        }

        private void RefreshPlaces()
        {
            for (int i = 0; i < places.Length; i++)
            {
                ValePlace place = ValePlaces.All[i];
                bool discovered = bootstrap.Discovery.IsDiscovered(place.Id);
                places[i].Name.text = discovered ? place.DisplayName : "?";
                places[i].Dot.color = places[i].Name.color = discovered ? Ink : Dim;
            }
        }

        private void RefreshDynamic()
        {
            occupied.Clear();
            QuestGoal goal = targets.Goal;
            instruction.text = goal == null ? "No current goal" : goal.Instruction;
            bool located = targets.TryLocate(goal, out Vector3 goalPosition);
            goalMarker.Root.gameObject.SetActive(located);
            if (located)
            {
                goalMarker.Root.anchoredPosition = Point(goalPosition.x, goalPosition.z);
                goalMarker.Name.text = goal.Label;
                Claim(goalMarker);
            }

            foreach (var place in places)
            {
                // Beside the dot, or under it when the goal's label is already there.
                place.Name.rectTransform.anchoredPosition = new Vector2(10f, 0f);
                if (!Free(place))
                {
                    place.Name.rectTransform.anchoredPosition = new Vector2(-4f, -16f);
                }

                Claim(place);
            }

            for (int i = 0; i < npcs.Length; i++)
            {
                NpcMarker npc = targets.Npcs[i];
                bool known = targets.IsKnown(npc);
                npcs[i].Root.gameObject.SetActive(known);
                if (known)
                {
                    Vector3 position = npc.Speaker.transform.position;
                    npcs[i].Root.anchoredPosition = Point(position.x, position.z);
                    npcs[i].Name.text = npc.Speaker.Descriptor.DisplayName;
                    npcs[i].Name.enabled = Free(npcs[i]);
                    if (npcs[i].Name.enabled)
                    {
                        Claim(npcs[i]);
                    }
                }
            }

            playerArrow.gameObject.SetActive(player != null);
            if (player != null)
            {
                Vector3 position = player.position, forward = player.forward;
                playerArrow.rectTransform.anchoredPosition = Point(position.x, position.z);
                float heading = NavigationMath.Heading(forward.x, forward.z);
                playerArrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -heading);
            }
        }

        private Rect LabelRect(Marker marker)
        {
            Vector2 at = marker.Root.anchoredPosition + marker.Name.rectTransform.anchoredPosition;
            return new Rect(at.x - 2f, at.y - 9f, marker.Name.preferredWidth + 4f, 18f);
        }

        private bool Free(Marker marker)
        {
            Rect label = LabelRect(marker);
            foreach (Rect taken in occupied)
            {
                if (taken.Overlaps(label))
                {
                    return false;
                }
            }

            return true;
        }

        private void Claim(Marker marker) => occupied.Add(LabelRect(marker));

        private Marker BuildMarker(string objectName, float size, int fontSize, Color colour)
        {
            RectTransform marker = Rect(objectName, map, Vector2.zero, Vector2.zero);
            RectTransform dot = Rect("Dot", marker, Vector2.zero, new Vector2(size, size));
            dot.pivot = new Vector2(0.5f, 0.5f);
            Text name = Label("Name", marker, new Vector2(10f, 0f), new Vector2(180f, 22f), fontSize, colour);
            name.rectTransform.pivot = new Vector2(0f, 0.5f);
            return new Marker { Root = marker, Dot = Paint(dot, colour), Name = name };
        }

        private static Vector2 Point(float x, float z)
        {
            var (u, v) = NavigationMath.ToMap(x, z, MapSize);
            return new Vector2(u, v);
        }

        private static RectTransform Rect(string objectName, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(objectName, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static Image Paint(RectTransform rect, Color colour)
        {
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = colour;
            image.raycastTarget = false;
            return image;
        }

        private Text Label(string objectName, Transform parent, Vector2 position, Vector2 size, int fontSize, Color colour)
        {
            Text label = Rect(objectName, parent, position, size).gameObject.AddComponent<Text>();
            label.font = font;
            label.fontSize = fontSize;
            label.color = colour;
            label.alignment = TextAnchor.MiddleLeft;
            label.supportRichText = false;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            var shadow = label.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.7f);
            return label;
        }

        private void OnDestroy()
        {
            if (root != null)
            {
                Destroy(root.gameObject);
            }
        }
    }
}
