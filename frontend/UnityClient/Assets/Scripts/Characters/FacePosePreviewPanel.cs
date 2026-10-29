using UnityEngine;

namespace A305.Characters
{
    // Sandbox presentation only. NPC speech can call CharacterFaceController directly.
    public sealed class FacePosePreviewPanel : MonoBehaviour
    {
        [SerializeField]
        private CharacterFaceController face;

        [SerializeField]
        private Camera viewCamera;

        [SerializeField]
        private Vector3 focus;

        [SerializeField]
        private Vector3 front;
        private CharacterFacePose pose;
        private float intensity = 1;
        private static readonly string[] Labels =
        {
            "Neutral",
            "Smile",
            "Eyes closed",
            "Mouth open",
            "F / V",
            "TH",
        };

        public void Configure(
            CharacterFaceController target,
            Camera camera,
            Vector3 center,
            Vector3 direction
        )
        {
            face = target;
            viewCamera = camera;
            focus = center;
            front = direction;
        }

        private void OnGUI()
        {
            if (face == null)
                return;
            GUILayout.BeginArea(new Rect(12, 12, 220, 310), GUI.skin.box);
            GUILayout.Label("Cafe Female - Face Preview");
            GUILayout.Label("Six poses / local mesh deformation");
            for (int i = 0; i < Labels.Length; i++)
                if (GUILayout.Button(Labels[i], GUILayout.Height(26)))
                {
                    pose = (CharacterFacePose)i;
                    face.SetPose(pose, intensity);
                }
            GUILayout.Label("Strength: " + Mathf.RoundToInt(intensity * 100) + "%");
            float value = GUILayout.HorizontalSlider(intensity, 0, 1);
            if (!Mathf.Approximately(value, intensity))
            {
                intensity = value;
                face.SetPose(pose, intensity);
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Left"))
                SetView(-30);
            if (GUILayout.Button("Front"))
                SetView(0);
            if (GUILayout.Button("Right"))
                SetView(30);
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void SetView(float degrees)
        {
            viewCamera.transform.position =
                focus + Quaternion.AngleAxis(degrees, Vector3.up) * front;
            viewCamera.transform.LookAt(focus);
        }
    }
}
