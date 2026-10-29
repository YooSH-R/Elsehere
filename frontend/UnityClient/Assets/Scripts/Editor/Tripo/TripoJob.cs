using System;

namespace A305.EditorTools.Tripo
{
    public enum TripoStage
    {
        Uploading,
        Generating,
        CheckingRig,
        Rigging,
        Downloading,
        ReadyToImport,
        Completed,
        Failed,
        Converting,
    }

    [Serializable]
    public sealed class TripoJob
    {
        public const string H31Model = "v3.1-20260211";
        public const string P2Model = "P2-20260801";

        public bool autoRig = true;
        public string conversionTaskId;
        public float conversionCredits;
        public TripoAssetKind kind;
        public float heightMeters = 1.7f;
        public string modelAssetPath;
        public bool modelConfigured;

        public static TripoJob Create(
            string name,
            TripoImages images,
            int faces,
            float height,
            string generationModel = H31Model
        )
        {
            if (
                string.IsNullOrWhiteSpace(name)
                || !System.Text.RegularExpressions.Regex.IsMatch(
                    name,
                    "^[A-Za-z][A-Za-z0-9_]{0,31}$"
                )
            )
                throw new ArgumentException(
                    "이름은 영문자로 시작하는 영문·숫자·밑줄 32자 이내로 입력하세요."
                );
            if (faces < 500 || faces > 50000)
                throw new ArgumentException("면 수는 500~50000 범위로 입력하세요.");
            if (float.IsNaN(height) || float.IsInfinity(height) || height < 0.1f || height > 10f)
                throw new ArgumentException("높이는 0.1~10m 범위로 입력하세요.");
            images.Validate();
            string id = Guid.NewGuid().ToString("N");
            string assetId = name + "_" + id.Substring(0, 8);
            return new TripoJob
            {
                assetName = name,
                model = generationModel,
                imagePaths = images.GetOrderedPaths(),
                faceLimit = faces,
                heightMeters = height,
                modelPath = "UserSettings/Tripo/Downloads/" + id + ".fbx",
                modelAssetPath = "Assets/Art/Characters/Tripo/" + assetId + "/Source.fbx",
                prefabPath = "Assets/Prefabs/Characters/Tripo/" + assetId + ".prefab",
            };
        }

        public void SetOutputFolders(string modelFolder, string prefabFolder)
        {
            if (
                stage != TripoStage.Uploading
                || submissionUncertain
                || !string.IsNullOrEmpty(generationTaskId)
            )
                throw new InvalidOperationException(
                    "생성 시작 전 작업만 저장 위치를 바꿀 수 있습니다."
                );
            string models = NormalizeOutputFolder(modelFolder);
            string prefabs = NormalizeOutputFolder(prefabFolder);
            string modelDirectory = System.IO.Path.GetFileName(
                System.IO.Path.GetDirectoryName(modelAssetPath)
            );
            string prefabName = System.IO.Path.GetFileName(prefabPath);
            modelAssetPath = models + "/" + modelDirectory + "/Source.fbx";
            prefabPath = prefabs + "/" + prefabName;
        }

        public static string NormalizeOutputFolder(string folder)
        {
            string normalized = (folder ?? "").Trim().Replace('\\', '/').TrimEnd('/');
            if (!normalized.StartsWith("Assets/", StringComparison.Ordinal))
                throw new ArgumentException("저장 폴더는 이 프로젝트의 Assets 하위여야 합니다.");
            foreach (string segment in normalized.Split('/'))
                if (
                    segment == ".."
                    || segment == "."
                    || segment.Length == 0
                    || segment.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0
                )
                    throw new ArgumentException("유효한 Assets 하위 저장 폴더를 선택하세요.");
            return normalized;
        }

        public string assetName;
        public string[] imagePaths;
        public string[] imageTokens = new string[4];
        public string model = H31Model;
        public int faceLimit = 15000;
        public TripoStage stage;
        public TripoStage progressStage;
        public string remoteStatus;
        public int remoteProgress;

        public bool TryGetRemoteProgress(out int progress)
        {
            progress = remoteProgress;
            return progressStage == stage
                && remoteStatus == "running"
                && (
                    stage == TripoStage.Generating
                    || stage == TripoStage.CheckingRig
                    || stage == TripoStage.Rigging
                    || stage == TripoStage.Converting
                );
        }

        public string generationTaskId;
        public string rigCheckTaskId;
        public string rigTaskId;
        public bool submissionUncertain;
        public int submittedStages;
        public string modelPath;
        public string prefabPath;
        public float generationCredits;
        public float rigCredits;
    }

    [Serializable]
    public sealed class TripoTask
    {
        public string task_id;
        public string status;
        public int progress;
        public int error_code;
        public string error_message;
        public float credits_consumed;
        public TripoOutput output;
    }

    [Serializable]
    public sealed class TripoOutput
    {
        public string model_url;
        public string rendered_image_url;
        public bool riggable;
        public string rig_type;
    }
}

