using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace A305.EditorTools.Tripo
{
    public sealed class TripoPoseStage : PreviewSceneStage
    {
        [SerializeField]
        private TripoAssetRecord record;
        private GameObject model;
        private Transform controls;
        private TripoPoseSession session;

        public static void Open(TripoAssetRecord record)
        {
            TripoModelReview.RequireHumanoid(
                AssetDatabase.LoadAssetAtPath<GameObject>(record.ModelPath)
            );
            var stage = CreateInstance<TripoPoseStage>();
            stage.record = record;
            StageUtility.GoToStage(stage, true);
        }

        protected override bool OnOpenStage()
        {
            if (!base.OnOpenStage())
                return false;
            model = TripoWorkbench.CreateAssembly(record);
            SceneManager.MoveGameObjectToScene(model, scene);
            var builder = TripoRigControls.BuildHumanoid(model);
            controls = builder.transform.Find("TripoControls");
            session = new TripoPoseSession(builder);
            EditorApplication.update += Evaluate;
            SceneView.duringSceneGui += DrawHandles;
            Selection.activeGameObject = model;
            SceneView.FrameLastActiveSceneView();
            return true;
        }

        protected override GUIContent CreateHeaderContent()
        {
            return new GUIContent("Tripo 자세 검수 · 원본 변경 없음");
        }

        protected override void OnCloseStage()
        {
            EditorApplication.update -= Evaluate;
            SceneView.duringSceneGui -= DrawHandles;
            session?.Dispose();
            session = null;
            base.OnCloseStage();
        }

        public void ApplyFace(
            TripoAssetRecord current,
            System.Collections.Generic.Dictionary<string, float> weights
        )
        {
            if (model != null && current == record)
                TripoFaceBindings.Apply(model, record.faceBindings, weights);
        }

        private void Evaluate()
        {
            if (StageUtility.GetCurrentStage() != this)
                return;
            session?.Evaluate();
        }

        private void DrawHandles(SceneView view)
        {
            if (controls == null || StageUtility.GetCurrentStage() != this)
                return;
            foreach (Transform limb in controls)
            foreach (Transform handle in limb)
            {
                bool rotationOnly = limb.name == "Head";
                Handles.color = handle.name == "Hint" ? new Color(1f, 0.7f, 0.2f) : Color.cyan;
                Handles.Label(handle.position, limb.name + " / " + handle.name);
                EditorGUI.BeginChangeCheck();
                Vector3 position = rotationOnly
                    ? handle.position
                    : Handles.PositionHandle(handle.position, Quaternion.identity);
                Quaternion rotation = rotationOnly
                    ? Handles.RotationHandle(handle.rotation, handle.position)
                    : handle.rotation;
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(handle, "Tripo pose handle");
                    handle.SetPositionAndRotation(position, rotation);
                    session.Evaluate();
                }
            }
        }
    }
}
