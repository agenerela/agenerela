using System.IO;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine.AI;

namespace Demos.GreyBoxVillage.Editor
{
    /// <summary>Imports text-serialized baked data. No rebaking occurs at import or in the player.</summary>
    [ScriptedImporter(1, "villagenavmesh")]
    public sealed class VillageNavMeshImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext context)
        {
            var data = new NavMeshData();
            EditorJsonUtility.FromJsonOverwrite(File.ReadAllText(context.assetPath), data);
            context.AddObjectToAsset("VillageNavMesh", data);
            context.SetMainObject(data);
        }
    }
}
