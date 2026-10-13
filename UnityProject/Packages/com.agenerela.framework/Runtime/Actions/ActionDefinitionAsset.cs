using UnityEngine;

namespace Agenerela
{
    /// <summary>
    /// Authoring wrapper around <see cref="ActionDefinition"/>. Its only jobs are to put
    /// the definition on disk as an asset a developer can fill in, and to warn about a
    /// malformed id while they type.
    /// </summary>
    [CreateAssetMenu(menuName = "Agenerela/Action", fileName = "NewAction")]
    public sealed class ActionDefinitionAsset : ScriptableObject
    {
        public ActionDefinition Action = new ActionDefinition();

        // Warns rather than throws: an asset is malformed for as long as it takes the
        // developer to finish typing into it, and that is not an error.
        private void OnValidate()
        {
            if (!ActionDefinition.ValidateId(Action.Id, out string problem))
            {
                Debug.LogWarning($"{name}: {problem}", this);
            }
        }
    }
}
