using System;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.Playables;

namespace A305.EditorTools.Tripo
{
    public sealed class TripoPoseSession : IDisposable
    {
        private readonly RigBuilder builder;
        private readonly Transform[] transforms;
        private readonly Vector3[] positions;
        private readonly Quaternion[] rotations;
        private readonly Vector3[] scales;
        private bool disposed;

        public TripoPoseSession(RigBuilder builder)
        {
            this.builder = builder;
            transforms = builder.GetComponentsInChildren<Transform>(true);
            positions = new Vector3[transforms.Length];
            rotations = new Quaternion[transforms.Length];
            scales = new Vector3[transforms.Length];
            for (int i = 0; i < transforms.Length; i++)
            {
                positions[i] = transforms[i].localPosition;
                rotations[i] = transforms[i].localRotation;
                scales[i] = transforms[i].localScale;
            }
            if (!builder.Build())
                throw new InvalidOperationException("자세 미리보기 그래프를 만들지 못했습니다.");
            builder.graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        }

        public void Evaluate()
        {
            if (!disposed && builder != null)
                builder.Evaluate(0f);
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            if (builder != null)
                builder.Clear();
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] == null)
                    continue;
                transforms[i].localPosition = positions[i];
                transforms[i].localRotation = rotations[i];
                transforms[i].localScale = scales[i];
            }
        }
    }
}
