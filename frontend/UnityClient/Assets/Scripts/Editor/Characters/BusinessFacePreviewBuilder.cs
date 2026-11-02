using System;

namespace A305.EditorTools.Characters
{
    public static class BusinessFacePreviewBuilder
    {
        private const string Source =
            "Assets/Art/Characters/ReviewCorrections/LittleMung_Business_Male/MasterRig/LittleMung_Business_Male_Visual.prefab";
        public const string Art =
            "Assets/Art/Characters/ReviewCorrections/LittleMung_Business_Male/Face";
        public const string Prefab =
            "Assets/Prefabs/Characters/Review/Corrected/LittleMung_Business_Male.prefab";
        public const string Scene =
            "Assets/Scenes/Sandbox/Characters/LittleMung_Business_Male_CorrectedReview.unity";

        private static CharacterFaceReviewBuilder Builder() =>
            new(
                new FaceReviewPaths
                {
                    Source = Source,
                    Art = Art,
                    Prefab = Prefab,
                    Scene = Scene,
                    SurfaceName = "BusinessFace",
                    Data = Environment.GetEnvironmentVariable("BUSINESS_FACE_DATA"),
                    Output = Environment.GetEnvironmentVariable("BUSINESS_REVIEW_OUTPUT"),
                }
            );

        public static void Build() => Builder().Build();

        public static void RefreshMouth() => Builder().RefreshMouth();

        public static void RefreshPreview() => Builder().RefreshPreview();

        public static void Render() => Builder().Render();

        public static void RefreshHeightAndAudit()
        {
            Builder().RefreshHeightAndAudit();
            BusinessCharacterReview.Export();
        }
    }
}
