using System.Collections.Generic;
using LivingVale.Unity;
using LivingVale.World;
using UnityEngine;
using UnityEngine.UI;

namespace LivingVale.UI
{
    /// <summary>
    /// G8a: a name and role over each speaking NPC near the player, by <see cref="NameTagRules"/>:
    /// within 15 m, fading from 10 m, never through a wall (a line from the camera to the NPC's head
    /// that meets anything but the NPC or the player hides the tag), and not while chatting with that
    /// NPC. Drawn on a screen-space canvas under every other HUD, so a tag never covers a panel.
    /// </summary>
    [DefaultExecutionOrder(410)]
    public sealed class NpcNameTags : MonoBehaviour
    {
        private const float HeadHeight = 2.25f;

        private readonly List<(NpcSpeaker npc, RectTransform tag, Text name, Text role)> tags =
            new List<(NpcSpeaker, RectTransform, Text, Text)>();
        private readonly RaycastHit[] hits = new RaycastHit[8];

        private Transform player;
        private RectTransform root;

        public void Initialize(Transform playerTransform, NavigationTargets targets)
        {
            player = playerTransform;
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -30;
            HudLayout.Scale(gameObject);
            root = (RectTransform)transform;
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            foreach (NpcMarker marker in targets.Npcs)
            {
                var tag = new GameObject("Name tag " + marker.Speaker.Descriptor.DisplayName, typeof(RectTransform))
                    .GetComponent<RectTransform>();
                tag.SetParent(root, false);
                tag.anchorMin = tag.anchorMax = Vector2.zero;
                tag.pivot = new Vector2(0.5f, 0f);
                tag.sizeDelta = new Vector2(160f, 38f);
                Text name = Label(tag, font, marker.Speaker.Descriptor.DisplayName, 17, Color.white, new Vector2(0f, 16f));
                Text role = Label(tag, font, marker.Speaker.Descriptor.Role, 12, new Color(0.8f, 0.85f, 0.9f), Vector2.zero);
                tag.gameObject.SetActive(false);
                tags.Add((marker.Speaker, tag, name, role));
            }
        }

        private void LateUpdate()
        {
            Camera view = Camera.main;
            if (view == null || player == null)
            {
                return;
            }

            float scale = root.lossyScale.x > 0f ? root.lossyScale.x : 1f;
            foreach (var (npc, tag, name, role) in tags)
            {
                if (npc == null)
                {
                    tag.gameObject.SetActive(false);
                    continue;
                }

                Vector3 head = npc.transform.position + Vector3.up * HeadHeight;
                float distance = Vector3.Distance(player.position, npc.transform.position);
                float alpha = 0f;
                Vector3 screen = Vector3.zero;
                if (distance <= NameTagRules.Range && !npc.IsTalking)
                {
                    screen = view.WorldToScreenPoint(head);
                    bool inFront = screen.z > 0f;
                    // The tag floats above the head; the sight line ends at the body, so a wall that
                    // hides the NPC hides the tag too.
                    alpha = inFront ? NameTagRules.Alpha(distance, Visible(view.transform.position, npc.TalkPoint, npc.transform), false) : 0f;
                }

                bool show = alpha > 0.01f;
                tag.gameObject.SetActive(show);
                if (!show)
                {
                    continue;
                }

                tag.anchoredPosition = new Vector2(screen.x / scale, screen.y / scale);
                SetAlpha(name, alpha);
                SetAlpha(role, alpha);
            }
        }

        private bool Visible(Vector3 eye, Vector3 head, Transform npc)
        {
            Vector3 line = head - eye;
            float length = line.magnitude;
            if (length < 0.01f)
            {
                return true;
            }

            int count = Physics.RaycastNonAlloc(eye, line / length, hits, length, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Transform hit = hits[i].transform;
                if (!hit.IsChildOf(npc) && !hit.IsChildOf(player))
                {
                    return false;
                }
            }

            return true;
        }

        private static void SetAlpha(Text text, float alpha)
        {
            Color colour = text.color;
            colour.a = alpha;
            text.color = colour;
        }

        private static Text Label(RectTransform parent, Font font, string value, int size, Color colour, Vector2 position)
        {
            var rect = new GameObject(value, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(160f, 20f);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.color = colour;
            text.text = value;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.supportRichText = false;
            text.raycastTarget = false;
            rect.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.75f);
            return text;
        }
    }
}
