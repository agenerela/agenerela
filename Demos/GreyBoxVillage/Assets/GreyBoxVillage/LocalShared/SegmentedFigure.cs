using UnityEngine;

namespace Demos.Shared
{
    /// <summary>
    /// Animates a segmented low-poly figure with no rig: legs and arms swing with the speed it is
    /// moving at, the body breathes when idle, and the head nods while it talks. The figure's parts
    /// are found by name under <see cref="Model"/> (torso, head, arm_l, arm_r, leg_l, leg_r for a
    /// person; torso, head, leg_fl, leg_fr, leg_bl, leg_br for a four-legged animal).
    /// </summary>
    /// <remarks>
    /// The parts arrive as siblings, each placed at its own joint, so on start the head and arms are
    /// re-parented to the torso: when the body bobs, they go with it. Movement is read from how far
    /// the figure travelled each frame, so it works the same under a NavMeshAgent, a
    /// CharacterController or a script.
    /// </remarks>
    public sealed class SegmentedFigure : MonoBehaviour
    {
        public Transform Model;
        [Tooltip("How far the limbs swing at a run, in degrees.")]
        public float Swing = 34f;
        [Tooltip("Steps per metre walked.")]
        public float StepsPerMetre = 1.3f;
        [Tooltip("The speed, in m/s, at which the swing is full.")]
        public float FullSwingSpeed = 3f;

        private Transform torso, head, armL, armR, legL, legR, legFL, legFR, legBL, legBR;
        private Quaternion torsoBase, headBase, armLBase, armRBase, legLBase, legRBase, fl, fr, bl, br;
        private Vector3 torsoPosition, lastPosition;
        private float phase, speed, talkUntil, wave;
        private bool bound;

        /// <summary>The figure's current walking speed, smoothed, in m/s.</summary>
        public float Speed => speed;

        /// <summary>Nods the head for a while, as if speaking.</summary>
        public void Talk(float seconds) => talkUntil = Time.time + seconds;

        /// <summary>Raises the right arm once, as a greeting or a call.</summary>
        public void Wave() => wave = 1f;

        private void Start() => Bind();

        private void Bind()
        {
            if (bound || Model == null) return;
            torso = Find("torso");
            head = Find("head");
            armL = Find("arm_l");
            armR = Find("arm_r");
            legL = Find("leg_l");
            legR = Find("leg_r");
            legFL = Find("leg_fl");
            legFR = Find("leg_fr");
            legBL = Find("leg_bl");
            legBR = Find("leg_br");
            if (torso == null) return;
            foreach (var part in new[] { head, armL, armR })
                if (part != null) part.SetParent(torso, true);
            torsoBase = torso.localRotation;
            torsoPosition = torso.localPosition;
            headBase = Base(head);
            armLBase = Base(armL);
            armRBase = Base(armR);
            legLBase = Base(legL);
            legRBase = Base(legR);
            fl = Base(legFL);
            fr = Base(legFR);
            bl = Base(legBL);
            br = Base(legBR);
            lastPosition = transform.position;
            bound = true;
        }

        private static Quaternion Base(Transform t) => t != null ? t.localRotation : Quaternion.identity;

        private Transform Find(string part)
        {
            foreach (var t in Model.GetComponentsInChildren<Transform>(true))
                if (t.name == part) return t;
            return null;
        }

        private void LateUpdate()
        {
            if (!bound) return;
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            var delta = transform.position - lastPosition;
            delta.y = 0;
            lastPosition = transform.position;
            speed = Mathf.Lerp(speed, delta.magnitude / dt, 1 - Mathf.Exp(-dt * 10));
            phase += delta.magnitude * StepsPerMetre * Mathf.PI;
            float amount = Mathf.Clamp01(speed / FullSwingSpeed);
            float s = Mathf.Sin(phase) * Swing * amount;
            float t = Time.time;

            // The body bobs with each step and breathes when still.
            float bob = Mathf.Abs(Mathf.Cos(phase)) * 0.05f * amount + Mathf.Sin(t * 2.1f) * 0.006f * (1 - amount);
            torso.localPosition = torsoPosition + Vector3.up * bob;
            torso.localRotation = torsoBase * Quaternion.Euler(4f * amount, 0, Mathf.Sin(phase) * 2f * amount);

            if (legL != null)
            {
                Swivel(legL, legLBase, s);
                Swivel(legR, legRBase, -s);
                wave = Mathf.MoveTowards(wave, 0, dt * 0.8f);
                float raise = Mathf.Sin(wave * Mathf.PI) * 150f;
                Swivel(armL, armLBase, -s * 0.8f);
                Swivel(armR, armRBase, s * 0.8f - raise);
            }
            else
            {
                Swivel(legFL, fl, s);
                Swivel(legBR, br, s);
                Swivel(legFR, fr, -s);
                Swivel(legBL, bl, -s);
            }

            if (head != null)
            {
                float nod = Time.time < talkUntil ? Mathf.Sin(t * 9f) * 5f : 0f;
                float look = Mathf.Sin(t * 0.37f + GetInstanceID()) * 6f * (1 - amount);
                head.localRotation = headBase * Quaternion.Euler(nod, look, 0);
            }
        }

        // Swings a limb about the model's right axis, as seen from the limb's parent.
        private void Swivel(Transform limb, Quaternion rest, float degrees)
        {
            if (limb == null) return;
            var axis = limb.parent.InverseTransformDirection(Model.right);
            limb.localRotation = Quaternion.AngleAxis(degrees, axis) * rest;
        }
    }
}
