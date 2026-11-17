using System.Collections.Generic;
using LivingVale.World;
using UnityEngine;

namespace LivingVale.Unity
{
    /// <summary>
    /// Walks an NPC through its <see cref="NpcRoutine"/> on flat ground. While the NPC's conversation is
    /// open the routine holds, and <see cref="NpcSpeaker"/> turns the NPC to the player instead, so the
    /// 3 m focus and 6 m range rules measure against an NPC that stays put.
    /// </summary>
    [RequireComponent(typeof(NpcSpeaker))]
    public sealed class NpcRoutineMover : MonoBehaviour
    {
        [SerializeField] private List<Vector3> stops = new List<Vector3>();

        [Tooltip("Idle seconds at each stop, by index; a missing entry idles for 4 s.")]
        [SerializeField] private List<float> pauses = new List<float>();

        [SerializeField, Min(0.1f)] private float speed = RegionNpcs.WalkSpeed;
        [SerializeField, Min(0f)] private float turnSpeed = 240f;

        private NpcSpeaker speaker;
        private NpcRoutine routine;

        public NpcRoutine Routine => routine;

        public void Configure(IReadOnlyList<RoutineStop> routineStops, float walkSpeed)
        {
            stops.Clear();
            pauses.Clear();
            foreach (var stop in routineStops)
            {
                stops.Add(new Vector3(stop.X, 0f, stop.Z));
                pauses.Add(stop.Pause);
            }

            speed = walkSpeed;
        }

        private void Awake()
        {
            speaker = GetComponent<NpcSpeaker>();
            var routineStops = new List<RoutineStop>();
            for (int i = 0; i < stops.Count; i++)
            {
                routineStops.Add(new RoutineStop(stops[i].x, stops[i].z, i < pauses.Count ? pauses[i] : 4f));
            }

            if (routineStops.Count == 0)
            {
                routineStops.Add(new RoutineStop(transform.position.x, transform.position.z, 0f));
            }

            routine = new NpcRoutine(routineStops, speed);
        }

        private void Update()
        {
            routine.Tick(Time.deltaTime, speaker.IsTalking);
            if (!routine.Moving)
            {
                return;
            }

            transform.position = new Vector3(routine.X, transform.position.y, routine.Z);
            var heading = new Vector3(routine.HeadingX, 0f, routine.HeadingZ);
            if (heading.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(heading), turnSpeed * Time.deltaTime);
            }
        }
    }
}
