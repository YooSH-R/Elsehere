using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace A305.EditorTools.Tripo
{
    public sealed class TripoModelPreview : IDisposable
    {
        private readonly PreviewRenderUtility preview;
        private readonly Quaternion referenceRotation;
        private Vector2 orbit;
        private float zoom = 1f;
        private readonly Bounds framingBounds;
        private readonly Material groundMaterial;
        public bool ShowSkeleton { get; set; }
        public GameObject Model { get; }

        public TripoModelPreview(GameObject model)
        {
            Model = model;
            referenceRotation = Quaternion.LookRotation(GetViewDirection(model, 0), Vector3.up);
            preview = new PreviewRenderUtility();
            preview.AddSingleGO(model);
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            framingBounds =
                renderers.Length == 0 ? new Bounds(Vector3.zero, Vector3.one) : renderers[0].bounds;
            foreach (var renderer in renderers)
                framingBounds.Encapsulate(renderer.bounds);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            UnityEngine.Object.DestroyImmediate(floor.GetComponent<Collider>());
            floor.name = "Rest pose ground guide";
            floor.hideFlags = HideFlags.HideAndDontSave;
            floor.transform.position = new Vector3(
                framingBounds.center.x,
                framingBounds.min.y,
                framingBounds.center.z
            );
            floor.transform.localScale = Vector3.one * Mathf.Max(framingBounds.size.y * 0.3f, 0.1f);
            groundMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            groundMaterial.SetColor("_BaseColor", new Color(0.3f, 0.32f, 0.34f));
            floor.GetComponent<Renderer>().sharedMaterial = groundMaterial;
            preview.AddSingleGO(floor);
            preview.camera.fieldOfView = 30f;
            preview.camera.clearFlags = CameraClearFlags.Color;
            preview.camera.backgroundColor = new Color(0.16f, 0.18f, 0.2f);
            preview.ambientColor = new Color(0.35f, 0.35f, 0.35f);
            preview.lights[0].color = Color.white;
            preview.lights[1].color = Color.white;
            preview.lights[0].intensity = 1.2f;
            preview.lights[0].transform.rotation = Quaternion.Euler(35f, 30f, 0f);
            preview.lights[1].intensity = 0.6f;
            preview.lights[1].transform.rotation = Quaternion.Euler(340f, 220f, 0f);
            foreach (var animator in model.GetComponentsInChildren<Animator>(true))
                animator.enabled = false;
            foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                skin.updateWhenOffscreen = true;
        }

        public static Vector3 GetViewDirection(GameObject model, int view) =>
            A305.EditorTools.Common.BuilderTools.GetCharacterViewDirection(model, view);

        public void SetView(int view)
        {
            float[] angles = { 0f, -90f, 90f, 180f };
            orbit = new Vector2(angles[Mathf.Clamp(view, 0, 3)], 0f);
        }

        public void Draw(Rect rect)
        {
            Event current = Event.current;
            if (rect.Contains(current.mousePosition))
            {
                if (current.type == EventType.MouseDrag)
                {
                    orbit += current.delta;
                    current.Use();
                    GUI.changed = true;
                }
                if (current.type == EventType.ScrollWheel)
                {
                    zoom = Mathf.Clamp(zoom + current.delta.y * 0.05f, 0.2f, 3f);
                    current.Use();
                    GUI.changed = true;
                }
            }
            if (current.type != EventType.Repaint)
                return;
            Bounds bounds = framingBounds;
            float radius = Mathf.Max(bounds.extents.magnitude, 0.01f);
            Quaternion rotation = referenceRotation * Quaternion.Euler(-orbit.y, orbit.x, 0f);
            preview.camera.transform.position =
                bounds.center + rotation * Vector3.forward * radius * 4f * zoom;
            preview.camera.transform.LookAt(bounds.center);
            preview.lights[0].transform.rotation =
                preview.camera.transform.rotation * Quaternion.Euler(25f, -30f, 0f);
            preview.lights[1].transform.rotation =
                preview.camera.transform.rotation * Quaternion.Euler(-20f, 40f, 0f);
            preview.camera.nearClipPlane = radius * 0.01f;
            preview.camera.farClipPlane = radius * 30f;
            preview.BeginPreview(rect, GUIStyle.none);
            preview.Render(true, true);
            GUI.DrawTexture(rect, preview.EndPreview(), ScaleMode.StretchToFill, false);
            if (ShowSkeleton)
                DrawSkeleton(rect);
        }

        private void DrawSkeleton(Rect rect)
        {
            var animator = Model.GetComponentInChildren<Animator>(true);
            if (
                animator == null
                || animator.avatar == null
                || !animator.avatar.isValid
                || !animator.isHuman
            )
                return;
            var bones = new HashSet<Transform>();
            for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
            {
                Transform bone = animator.GetBoneTransform((HumanBodyBones)i);
                if (bone != null)
                    bones.Add(bone);
            }
            Color previous = Handles.color;
            Handles.color = Color.cyan;
            GUI.BeginGroup(rect);
            foreach (Transform bone in bones)
            {
                Transform parent = bone.parent;
                while (parent != null && !bones.Contains(parent))
                    parent = parent.parent;
                if (parent == null)
                    continue;
                Vector3 first = preview.camera.WorldToViewportPoint(parent.position);
                Vector3 second = preview.camera.WorldToViewportPoint(bone.position);
                if (first.z <= 0f || second.z <= 0f)
                    continue;
                Handles.DrawAAPolyLine(
                    2f,
                    new Vector3(first.x * rect.width, (1f - first.y) * rect.height),
                    new Vector3(second.x * rect.width, (1f - second.y) * rect.height)
                );
            }
            GUI.EndGroup();
            Handles.color = previous;
        }

        public void Dispose()
        {
            preview.Cleanup();
            UnityEngine.Object.DestroyImmediate(groundMaterial);
        }
    }
}
