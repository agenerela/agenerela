using System;
using System.Collections.Generic;

namespace LivingVale.World
{
    /// <summary>One routine waypoint: where the NPC stands, on the ground plane, and how long it idles there.</summary>
    public readonly struct RoutineStop
    {
        public float X { get; }
        public float Z { get; }

        /// <summary>Seconds of idle work at this stop before walking on.</summary>
        public float Pause { get; }

        public RoutineStop(float x, float z, float pause)
        {
            X = x;
            Z = z;
            Pause = pause < 0f ? 0f : pause;
        }
    }

    /// <summary>
    /// A deterministic walk-and-idle loop through two or three stops (design §7: "two or three
    /// routine waypoints per settlement and idle work"). Flat ground, straight lines, no NavMesh:
    /// the stops are authored with a clear line between them. While <c>held</c> is true, for an open
    /// conversation, the NPC stays exactly where it is and resumes the same leg afterwards.
    /// </summary>
    /// <remarks>Plain C#, no scene; <c>NpcRoutineMover</c> copies the position onto a Transform.</remarks>
    public sealed class NpcRoutine
    {
        private readonly RoutineStop[] stops;

        public NpcRoutine(IReadOnlyList<RoutineStop> routineStops, float speed)
        {
            if (routineStops == null || routineStops.Count == 0)
            {
                throw new ArgumentException("A routine needs at least one stop.", nameof(routineStops));
            }

            if (!(speed > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(speed), "Walking speed must be positive.");
            }

            stops = new RoutineStop[routineStops.Count];
            for (int i = 0; i < stops.Length; i++)
            {
                stops[i] = routineStops[i];
            }

            Speed = speed;
            X = stops[0].X;
            Z = stops[0].Z;
            PauseLeft = stops[0].Pause;
        }

        public float Speed { get; }
        public float X { get; private set; }
        public float Z { get; private set; }

        /// <summary>The stop the NPC is at or walking to.</summary>
        public int StopIndex { get; private set; }

        /// <summary>Idle time left at the current stop; 0 while walking.</summary>
        public float PauseLeft { get; private set; }

        /// <summary>Whether the last tick moved the NPC. A mover turns it to face <see cref="HeadingX"/>, <see cref="HeadingZ"/> only then.</summary>
        public bool Moving { get; private set; }

        /// <summary>The unit direction of the last step.</summary>
        public float HeadingX { get; private set; }
        public float HeadingZ { get; private set; }

        public int StopCount => stops.Length;

        /// <summary>Advances the routine by <paramref name="deltaTime"/> seconds, unless <paramref name="held"/>.</summary>
        public void Tick(float deltaTime, bool held)
        {
            Moving = false;
            if (held || stops.Length < 2 || !(deltaTime > 0f))
            {
                return;
            }

            if (PauseLeft > 0f)
            {
                PauseLeft -= deltaTime;
                if (PauseLeft > 0f)
                {
                    return;
                }

                // Idling ended this tick; the next leg starts on the next one.
                PauseLeft = 0f;
                StopIndex = (StopIndex + 1) % stops.Length;
                return;
            }

            RoutineStop target = stops[StopIndex];
            float dx = target.X - X, dz = target.Z - Z;
            float distance = (float)Math.Sqrt(dx * dx + dz * dz);
            float step = Speed * deltaTime;
            if (distance <= step)
            {
                X = target.X;
                Z = target.Z;
                Moving = distance > 0f;
                PauseLeft = target.Pause;
                if (PauseLeft <= 0f)
                {
                    StopIndex = (StopIndex + 1) % stops.Length;
                }
            }
            else
            {
                X += dx / distance * step;
                Z += dz / distance * step;
                Moving = true;
            }

            if (distance > 0f)
            {
                HeadingX = dx / distance;
                HeadingZ = dz / distance;
            }
        }
    }
}
