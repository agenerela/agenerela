using Agenerela;
using UnityEngine;

namespace Demos.GreyBoxVillage
{
    /// <summary>
    /// The guard who answers trouble around the village: <c>chase</c> a thief or the wolf, and
    /// <c>fetch_water</c> for a fire. Wren carries it; Bram does not.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="AgentBehaviour"/> registers the <c>[AgentAction]</c> methods of every component on
    /// its GameObject, so these two actions are Wren's alone, as the gate's are (<see cref="NightWatch"/>).
    /// Bram is the evaluation agent, and #153 keeps his actions as they were: offered a new action
    /// beside his own, the model took it over the bell for the wolf (four live runs, 5 November).
    /// He goes after a running thief with <c>attack_target</c>, which runs after it, and a test checks
    /// that whatever happens he is offered nothing outside his own set.
    /// </para>
    /// <para>
    /// The running and the bucket trips are the guard's own movement, in <see cref="VillageGuard"/>;
    /// this component only decides when they are on offer.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(VillageGuard))]
    public sealed class Responder : MonoBehaviour
    {
        public VillageGuard Guard;

        [AgentAction("chase", "Run after a thief or the wolf until you catch it.", RequiresTarget = true)]
        [Example("Don't let the {0} get away!", "thief", "wolf")]
        private void Chase(Transform target) => Guard.Chase(target);

        // Something to run after must be in sight: a thief making off, or the wolf.
        [Available("chase")]
        private bool CanChase(AgentContext ctx) =>
            AgentMind.Moment.Of(ctx) is { } moment && (moment.WolfMetres != null || moment.ThiefMetres != null);

        [AgentAction("fetch_water", "Carry buckets from the well to the fire until it is out.")]
        [Example("Flames are climbing up the side of the barn!")]
        private void FetchWater() => Guard.FetchWater();

        [Available("fetch_water")]
        private bool CanFetchWater(AgentContext ctx) => Guard.Fire != null && Guard.Fire.IsBurning;
    }
}
