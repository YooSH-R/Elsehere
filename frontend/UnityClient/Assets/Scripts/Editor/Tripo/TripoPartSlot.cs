using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Tripo
{
    [Serializable]
    public sealed class TripoPartSlot
    {
        public string name = "Accessory";
        public string bonePath;
        public GameObject model;
        public TripoAssetKind category = TripoAssetKind.Accessory;
        public TripoAttachmentKind attachment = TripoAttachmentKind.Rigid;
        public Vector3 position;
        public Vector3 rotation;
        public float scale = 1f;
    }

    public static class TripoParts
    {
        public static GameObject Attach(GameObject character, TripoPartSlot slot)
        {
            if (character == null || slot == null || slot.model == null)
                throw new InvalidOperationException("캐릭터와 부품 모델이 필요합니다.");
            if (
                !Finite(slot.scale)
                || slot.scale <= 0f
                || !Finite(slot.position)
                || !Finite(slot.rotation)
            )
                throw new InvalidOperationException(
                    "부품 위치·회전은 유한값, 크기는 양수여야 합니다."
                );
            if (slot.attachment == TripoAttachmentKind.FaceSurface)
                throw new InvalidOperationException(
                    "얼굴 표면 추종은 아직 지원하지 않습니다. 얼굴에 함께 스킨/변형한 메시로 준비하세요."
                );
            if (slot.attachment == TripoAttachmentKind.Skinned)
                return AttachSkin(character, slot);
            Transform parent = string.IsNullOrEmpty(slot.bonePath)
                ? character.transform
                : character.transform.Find(slot.bonePath);
            if (parent == null)
                throw new InvalidOperationException("소켓 본을 찾을 수 없습니다: " + slot.bonePath);
            GameObject instance = Object.Instantiate(slot.model, parent, false);
            instance.name = slot.name;
            instance.transform.localPosition = slot.position;
            instance.transform.localRotation = Quaternion.Euler(slot.rotation);
            instance.transform.localScale = Vector3.one * slot.scale;
            return instance;
        }

        private static GameObject AttachSkin(GameObject character, TripoPartSlot slot)
        {
            if (slot.position != Vector3.zero || slot.rotation != Vector3.zero || slot.scale != 1f)
                throw new InvalidOperationException(
                    "스킨 부품은 공통 기준 자세·크기로 준비하세요. 개별 오프셋은 Blender에서 수정합니다."
                );
            GameObject instance = Object.Instantiate(slot.model, character.transform, false);
            instance.name = slot.name;
            try
            {
                var skins = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if (skins.Length == 0)
                    throw new InvalidOperationException(
                        "스킨 부품에 SkinnedMeshRenderer가 없습니다."
                    );
                foreach (var skin in skins)
                {
                    if (
                        skin.sharedMesh == null
                        || skin.sharedMesh.bindposes.Length != skin.bones.Length
                    )
                        throw new InvalidOperationException(
                            "부품의 바인드 포즈와 본 수가 다릅니다."
                        );
                    Transform[] mapped = new Transform[skin.bones.Length];
                    Matrix4x4[] poses = skin.sharedMesh.bindposes;
                    for (int i = 0; i < mapped.Length; i++)
                    {
                        mapped[i] = MapBone(skin.bones[i], instance.transform, character.transform);
                        Matrix4x4 expected =
                            mapped[i].worldToLocalMatrix * skin.transform.localToWorldMatrix;
                        for (int cell = 0; cell < 16; cell++)
                            if (
                                !Finite(poses[i][cell])
                                || Mathf.Abs(expected[cell] - poses[i][cell]) > 0.0001f
                            )
                                throw new InvalidOperationException(
                                    "공통 기준 자세와 다른 바인드 포즈: " + mapped[i].name
                                );
                    }
                    skin.rootBone = MapBone(skin.rootBone, instance.transform, character.transform);
                    skin.bones = mapped;
                }
                foreach (var animator in instance.GetComponentsInChildren<Animator>(true))
                    Object.DestroyImmediate(animator);
                return instance;
            }
            catch
            {
                Object.DestroyImmediate(instance);
                throw;
            }
        }

        private static Transform MapBone(Transform bone, Transform source, Transform target)
        {
            if (bone == null || (bone != source && !bone.IsChildOf(source)))
                throw new InvalidOperationException("부품 안에 연결되지 않은 본이 있습니다.");
            string path = AnimationUtility.CalculateTransformPath(bone, source);
            Transform mapped = string.IsNullOrEmpty(path) ? target : target.Find(path);
            if (mapped == null)
                throw new InvalidOperationException("기준 골격에 같은 본 경로가 없습니다: " + path);
            return mapped;
        }

        private static bool Finite(Vector3 value) =>
            Finite(value.x) && Finite(value.y) && Finite(value.z);

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
