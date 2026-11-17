using UnityEngine;
using UnityEngine.AI;

namespace Demos.GreyBoxVillage
{
    /// <summary>Registers the editor-baked data before the guard is enabled. No runtime bake.</summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class VillageNavigation : MonoBehaviour
    {
        public NavMeshData BakedData;
        private NavMeshDataInstance instance;

        private void OnEnable()
        {
            if (BakedData != null) instance = NavMesh.AddNavMeshData(BakedData);
            else Debug.LogError("GreyBoxVillage is missing its baked NavMesh.", this);
        }

        private void OnDisable()
        {
            if (instance.valid) instance.Remove();
        }
    }
}
