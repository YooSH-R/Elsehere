using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Characters
{
    // Geometric head sizing on an instantiated review copy; skeleton and body remain shared.
    public static class CharacterHeadProportions
    {
        private sealed class Surface
        {
            public SkinnedMeshRenderer skin;
            public Vector3[] world;
            public float[] influence;
            public Matrix4x4[] rest;
        }

        public static float Ratio(GameObject model) => Ratio(Capture(model), Vector3.zero, 1);

        public static Vector3 MatchDimensions(GameObject candidate, GameObject reference)
        {
            if (EditorUtility.IsPersistent(candidate))
                throw new ArgumentException("머리 치수는 생성한 복사본에서만 변경할 수 있습니다.");
            Bounds from = HeadBounds(candidate),
                to = HeadBounds(reference);
            var scale = new Vector3(
                to.size.x / from.size.x,
                to.size.y / from.size.y,
                to.size.z / from.size.z
            );
            if (
                (scale - Vector3.one).sqrMagnitude < .00000001f
                && (from.center - to.center).sqrMagnitude < .00000001f
            )
                return Vector3.one;
            if (
                !float.IsFinite(scale.x + scale.y + scale.z)
                || Mathf.Min(scale.x, scale.y, scale.z) < .5f
                || Mathf.Max(scale.x, scale.y, scale.z) > 2
            )
                throw new InvalidOperationException("머리 치수 보정 범위는 원본의 0.5~2배입니다.");
            Matrix4x4 world =
                Matrix4x4.Translate(to.center)
                * Matrix4x4.Scale(scale)
                * Matrix4x4.Translate(-from.center);
            var surfaces = Capture(candidate);
            // 목과 섞인 가중치에도 목표 외곽이 맞도록 같은 원본 정점에서 보정량을 수렴시킨다.
            for (int iteration = 0; iteration < 16; iteration++)
            {
                var points = surfaces
                    .SelectMany(s => s.world.Select((p, i) => (p, weight: s.influence[i])))
                    .Where(v => v.weight > .8f)
                    .Select(v => Vector3.Lerp(v.p, world.MultiplyPoint3x4(v.p), v.weight))
                    .ToArray();
                var measured = new Bounds(points[0], Vector3.zero);
                foreach (Vector3 point in points)
                    measured.Encapsulate(point);
                if (
                    (measured.size - to.size).sqrMagnitude < .0000000001f
                    && (measured.center - to.center).sqrMagnitude < .0000000001f
                )
                    break;
                var correction = new Vector3(
                    to.size.x / measured.size.x,
                    to.size.y / measured.size.y,
                    to.size.z / measured.size.z
                );
                world =
                    Matrix4x4.Translate(to.center)
                    * Matrix4x4.Scale(correction)
                    * Matrix4x4.Translate(-measured.center)
                    * world;
            }
            scale = new Vector3(world.m00, world.m11, world.m22);
            foreach (var surface in surfaces)
            {
                Mesh original = surface.skin.sharedMesh;
                Mesh mesh = Object.Instantiate(original);
                var vertices = mesh.vertices;
                var normals = mesh.normals;
                var tangents = mesh.tangents;
                var transforms = new Matrix4x4[vertices.Length];
                var normalTransforms = new Matrix4x4[vertices.Length];
                for (int i = 0; i < vertices.Length; i++)
                {
                    Matrix4x4 blended = Matrix4x4.identity;
                    for (int entry = 0; entry < 16; entry++)
                        blended[entry] = Mathf.Lerp(
                            blended[entry],
                            world[entry],
                            surface.influence[i]
                        );
                    transforms[i] = surface.rest[i].inverse * blended * surface.rest[i];
                    normalTransforms[i] = transforms[i].inverse.transpose;
                    // 몸통 정점은 부동소수점 재계산조차 하지 않아 그대로 보존한다.
                    if (surface.influence[i] == 0)
                        continue;
                    vertices[i] = transforms[i].MultiplyPoint3x4(vertices[i]);
                    if (normals.Length == vertices.Length)
                        normals[i] = normalTransforms[i].MultiplyVector(normals[i]).normalized;
                    if (tangents.Length == vertices.Length)
                    {
                        Vector3 direction = transforms[i].MultiplyVector(tangents[i]).normalized;
                        tangents[i] = new Vector4(
                            direction.x,
                            direction.y,
                            direction.z,
                            tangents[i].w
                        );
                    }
                }
                mesh.vertices = vertices;
                mesh.normals = normals;
                mesh.tangents = tangents;
                mesh.ClearBlendShapes();
                var delta = new Vector3[mesh.vertexCount];
                var normal = new Vector3[mesh.vertexCount];
                var tangent = new Vector3[mesh.vertexCount];
                var baseNormals = original.normals;
                var baseTangents = original.tangents;
                for (int shape = 0; shape < original.blendShapeCount; shape++)
                for (int frame = 0; frame < original.GetBlendShapeFrameCount(shape); frame++)
                {
                    original.GetBlendShapeFrameVertices(shape, frame, delta, normal, tangent);
                    for (int i = 0; i < delta.Length; i++)
                    {
                        if (surface.influence[i] == 0)
                            continue;
                        delta[i] = transforms[i].MultiplyVector(delta[i]);
                        if (baseNormals.Length == delta.Length)
                            normal[i] =
                                normalTransforms[i]
                                    .MultiplyVector(baseNormals[i] + normal[i])
                                    .normalized - normals[i];
                        if (baseTangents.Length == delta.Length)
                            tangent[i] =
                                transforms[i]
                                    .MultiplyVector((Vector3)baseTangents[i] + tangent[i])
                                    .normalized - (Vector3)tangents[i];
                    }
                    mesh.AddBlendShapeFrame(
                        original.GetBlendShapeName(shape),
                        original.GetBlendShapeFrameWeight(shape, frame),
                        delta,
                        normal,
                        tangent
                    );
                }
                mesh.RecalculateBounds();
                surface.skin.sharedMesh = mesh;
                surface.skin.localBounds = mesh.bounds;
            }
            return scale;
        }

        // 모자 높이만 맞추면 좁은 얼굴이 남을 수 있어, 좌우 폭/머리 높이를 별도로 맞춘다.
        public static float MatchWidth(GameObject candidate, GameObject reference)
        {
            if (EditorUtility.IsPersistent(candidate))
                throw new ArgumentException(
                    "Instantiate a review copy before changing proportions."
                );
            var surfaces = Capture(candidate);
            var rig = candidate.GetComponentInChildren<Animator>();
            var axis = WidthAxis(rig);
            float target = WidthRatio(
                Capture(reference),
                WidthAxis(reference.GetComponentInChildren<Animator>()),
                Vector3.zero,
                1
            );
            var pivot = rig.GetBoneTransform(HumanBodyBones.Neck).position;
            if (
                !float.IsFinite(target)
                || WidthRatio(surfaces, axis, pivot, .5f) > target
                || WidthRatio(surfaces, axis, pivot, 2) < target
            )
                throw new InvalidOperationException(
                    "Reference head width is outside the supported range."
                );
            float low = .5f,
                high = 2;
            for (int i = 0; i < 32; i++)
            {
                float middle = (low + high) * .5f;
                if (WidthRatio(surfaces, axis, pivot, middle) < target)
                    low = middle;
                else
                    high = middle;
            }
            float scale = (low + high) * .5f;
            if (Mathf.Abs(scale - 1) < .00001f)
                return 1;
            foreach (var surface in surfaces)
            {
                var original = surface.skin.sharedMesh;
                var mesh = Object.Instantiate(original);
                var vertices = mesh.vertices;
                var normals = mesh.normals;
                var transforms = new Matrix4x4[vertices.Length];
                var normalTransforms = new Matrix4x4[vertices.Length];
                for (int i = 0; i < vertices.Length; i++)
                {
                    float amount = (scale - 1) * surface.influence[i];
                    var world = Matrix4x4.identity;
                    for (int row = 0; row < 3; row++)
                    for (int column = 0; column < 3; column++)
                        world[row, column] += amount * axis[row] * axis[column];
                    transforms[i] = surface.rest[i].inverse * world * surface.rest[i];
                    normalTransforms[i] = transforms[i].inverse.transpose;
                    var shift = axis * (Vector3.Dot(surface.world[i] - pivot, axis) * amount);
                    vertices[i] += surface.rest[i].inverse.MultiplyVector(shift);
                    if (normals.Length == vertices.Length)
                        normals[i] = normalTransforms[i].MultiplyVector(normals[i]).normalized;
                }
                mesh.vertices = vertices;
                if (normals.Length == vertices.Length)
                    mesh.normals = normals;
                var tangents = mesh.tangents;
                for (int i = 0; i < tangents.Length; i++)
                {
                    var direction = transforms[i].MultiplyVector(tangents[i]).normalized;
                    tangents[i] = new Vector4(direction.x, direction.y, direction.z, tangents[i].w);
                }
                mesh.tangents = tangents;
                mesh.ClearBlendShapes();
                var delta = new Vector3[mesh.vertexCount];
                var normal = new Vector3[mesh.vertexCount];
                var tangent = new Vector3[mesh.vertexCount];
                var sourceNormals = original.normals;
                var sourceTangents = original.tangents;
                for (int shape = 0; shape < original.blendShapeCount; shape++)
                for (int frame = 0; frame < original.GetBlendShapeFrameCount(shape); frame++)
                {
                    original.GetBlendShapeFrameVertices(shape, frame, delta, normal, tangent);
                    for (int i = 0; i < delta.Length; i++)
                    {
                        delta[i] = transforms[i].MultiplyVector(delta[i]);
                        normal[i] =
                            sourceNormals.Length == delta.Length
                                ? normalTransforms[i]
                                    .MultiplyVector(sourceNormals[i] + normal[i])
                                    .normalized - normals[i]
                                : Vector3.zero;
                        tangent[i] =
                            sourceTangents.Length == delta.Length
                                ? transforms[i]
                                    .MultiplyVector((Vector3)sourceTangents[i] + tangent[i])
                                    .normalized - (Vector3)tangents[i]
                                : Vector3.zero;
                    }
                    mesh.AddBlendShapeFrame(
                        original.GetBlendShapeName(shape),
                        original.GetBlendShapeFrameWeight(shape, frame),
                        delta,
                        normal,
                        tangent
                    );
                }
                mesh.RecalculateBounds();
                surface.skin.sharedMesh = mesh;
                surface.skin.localBounds = mesh.bounds;
            }
            return scale;
        }

        private static Vector3 WidthAxis(Animator rig) =>
            Vector3
                .ProjectOnPlane(
                    rig.GetBoneTransform(HumanBodyBones.RightUpperLeg).position
                        - rig.GetBoneTransform(HumanBodyBones.LeftUpperLeg).position,
                    Vector3.up
                )
                .normalized;

        private static float WidthRatio(
            Surface[] surfaces,
            Vector3 axis,
            Vector3 pivot,
            float scale
        )
        {
            var points = surfaces
                .SelectMany(s =>
                    s.world.Select((p, i) => (point: p, weight: s.influence[i]))
                        .Where(v => v.weight > .8f)
                )
                .ToArray();
            var widths = points
                .Select(v =>
                    Vector3.Dot(v.point, axis)
                    + Vector3.Dot(v.point - pivot, axis) * (scale - 1) * v.weight
                )
                .ToArray();
            float width = widths.Max() - widths.Min();
            return width / (points.Max(v => v.point.y) - points.Min(v => v.point.y));
        }

        public static Bounds HeadBounds(GameObject model)
        {
            var points = Capture(model)
                .SelectMany(s => s.world.Where((v, i) => s.influence[i] > .8f))
                .ToArray();
            if (points.Length == 0)
                throw new InvalidOperationException("No head surface.");
            var bounds = new Bounds(points[0], Vector3.zero);
            foreach (var point in points)
                bounds.Encapsulate(point);
            return bounds;
        }

        public static float Match(GameObject candidate, GameObject reference)
        {
            if (EditorUtility.IsPersistent(candidate))
                throw new ArgumentException(
                    "Instantiate a review copy before changing proportions."
                );
            var surfaces = Capture(candidate);
            var pivot = candidate
                .GetComponentInChildren<Animator>()
                .GetBoneTransform(HumanBodyBones.Neck)
                .position;
            float target = Ratio(reference);
            float low = .4f,
                high = 3f;
            if (Ratio(surfaces, pivot, low) > target || Ratio(surfaces, pivot, high) < target)
                throw new InvalidOperationException(
                    "Reference head proportion is outside the supported range."
                );
            for (int i = 0; i < 32; i++)
            {
                float middle = (low + high) * .5f;
                if (Ratio(surfaces, pivot, middle) < target)
                    low = middle;
                else
                    high = middle;
            }
            float scale = (low + high) * .5f;
            foreach (var surface in surfaces)
            {
                var original = surface.skin.sharedMesh;
                var mesh = Object.Instantiate(original);
                mesh.name = original.name + "_LittleMungProportion";
                var vertices = mesh.vertices;
                for (int i = 0; i < vertices.Length; i++)
                {
                    var shift = (surface.world[i] - pivot) * (scale - 1) * surface.influence[i];
                    vertices[i] += surface.rest[i].inverse.MultiplyVector(shift);
                }
                mesh.vertices = vertices;
                mesh.ClearBlendShapes();
                var delta = new Vector3[mesh.vertexCount];
                var normal = new Vector3[mesh.vertexCount];
                var tangent = new Vector3[mesh.vertexCount];
                for (int shape = 0; shape < original.blendShapeCount; shape++)
                for (int frame = 0; frame < original.GetBlendShapeFrameCount(shape); frame++)
                {
                    original.GetBlendShapeFrameVertices(shape, frame, delta, normal, tangent);
                    for (int i = 0; i < delta.Length; i++)
                        delta[i] *= 1 + (scale - 1) * surface.influence[i];
                    mesh.AddBlendShapeFrame(
                        original.GetBlendShapeName(shape),
                        original.GetBlendShapeFrameWeight(shape, frame),
                        delta,
                        normal,
                        tangent
                    );
                }
                mesh.RecalculateBounds();
                surface.skin.sharedMesh = mesh;
                surface.skin.localBounds = mesh.bounds;
            }
            return scale;
        }

        private static Surface[] Capture(GameObject model)
        {
            var rig = model.GetComponentInChildren<Animator>();
            if (rig == null || !rig.isHuman)
                throw new ArgumentException("A Humanoid character is required.");
            var head = rig.GetBoneTransform(HumanBodyBones.Head);
            return model
                .GetComponentsInChildren<SkinnedMeshRenderer>()
                .Select(skin =>
                {
                    var mesh = skin.sharedMesh;
                    var weights = mesh.boneWeights;
                    var positions = mesh.vertices;
                    var matrices = skin
                        .bones.Select((bone, i) => bone.localToWorldMatrix * mesh.bindposes[i])
                        .ToArray();
                    var surface = new Surface
                    {
                        skin = skin,
                        world = new Vector3[positions.Length],
                        influence = new float[positions.Length],
                        rest = new Matrix4x4[positions.Length],
                    };
                    for (int i = 0; i < positions.Length; i++)
                    {
                        var w = weights[i];
                        int[] ids = { w.boneIndex0, w.boneIndex1, w.boneIndex2, w.boneIndex3 };
                        float[] amounts = { w.weight0, w.weight1, w.weight2, w.weight3 };
                        var matrix = Matrix4x4.zero;
                        for (int k = 0; k < 4; k++)
                        {
                            for (int entry = 0; entry < 16; entry++)
                                matrix[entry] += matrices[ids[k]][entry] * amounts[k];
                            if (skin.bones[ids[k]] == head || skin.bones[ids[k]].IsChildOf(head))
                                surface.influence[i] += amounts[k];
                        }
                        surface.rest[i] = matrix;
                        surface.world[i] = matrix.MultiplyPoint3x4(positions[i]);
                    }
                    return surface;
                })
                .ToArray();
        }

        private static float Ratio(Surface[] surfaces, Vector3 pivot, float scale)
        {
            float bottom = float.PositiveInfinity,
                top = float.NegativeInfinity;
            float chin = float.PositiveInfinity,
                crown = float.NegativeInfinity;
            foreach (var surface in surfaces)
                for (int i = 0; i < surface.world.Length; i++)
                {
                    float y =
                        surface.world[i].y
                        + (surface.world[i].y - pivot.y) * (scale - 1) * surface.influence[i];
                    bottom = Mathf.Min(bottom, y);
                    top = Mathf.Max(top, y);
                    if (surface.influence[i] > .8f)
                    {
                        chin = Mathf.Min(chin, y);
                        crown = Mathf.Max(crown, y);
                    }
                }
            if (!float.IsFinite(chin) || top - bottom < .001f || crown - chin < .001f)
                throw new InvalidOperationException(
                    "Head and body surfaces could not be measured."
                );
            return (crown - chin) / (top - bottom);
        }
    }
}
