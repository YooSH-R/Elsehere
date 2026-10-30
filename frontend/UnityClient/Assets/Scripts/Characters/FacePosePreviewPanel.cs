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
        private Vector2 scroll;
        private static readonly (CharacterFacePose Pose, string Label)[] Options =
        {
            (CharacterFacePose.Neutral, "01 REST / SIL"),
            (CharacterFacePose.ClosedLips, "02 M / B / P"),
            (CharacterFacePose.OpenMouth, "03 AA / AH"),
            (CharacterFacePose.E, "04 E / EH"),
            (CharacterFacePose.OH, "05 OH"),
            (CharacterFacePose.OU, "06 OU / OO"),
            (CharacterFacePose.FV, "07 F / V"),
            (CharacterFacePose.TH, "08 TH"),
            (CharacterFacePose.WideI, "09 I (ee)"),
            (CharacterFacePose.FlatEu, "10 EU"),
            (CharacterFacePose.Smile, "Smile"),
            (CharacterFacePose.Blink, "Eyes closed"),
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
            GUILayout.BeginArea(
                new Rect(12, 12, 240, Mathf.Min(520, Screen.height - 24)),
                GUI.skin.box
            );
            GUILayout.Label(face.gameObject.name + " - Face Preview");
            GUILayout.Label("10 speech mouths + smile / blink");
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.ExpandHeight(true));
            foreach (var option in Options)
                if (GUILayout.Button(option.Label, GUILayout.Height(26)))
                {
                    pose = option.Pose;
                    face.SetPose(pose, intensity);
                }
            GUILayout.EndScrollView();
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
