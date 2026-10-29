using A305.EditorTools.Mission;
using A305.EditorTools.ObjectHover;
using A305.EditorTools.Theme;
using A305.EditorTools.World;
using UnityEditor;

namespace A305.EditorTools.UI
{
    public static class TownPresentationBuilder
    {
        [MenuItem("Tools/A305/UI/Build Town Presentation")]
        public static void Build()
        {
            SpaceSceneSetup.PrepareFeatureAssets();
            FirstDayMissionBuilder.Build();
            UiThemeBuilder.Build();
            ObjectWordRegistration.Build();
            AssetDatabase.SaveAssets();
            Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll();
        }
    }
}
