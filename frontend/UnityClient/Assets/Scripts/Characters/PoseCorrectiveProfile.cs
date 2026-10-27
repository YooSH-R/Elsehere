using System;
using System.Collections.Generic;
using UnityEngine;

namespace A305.Characters
{
    [Serializable]
    public sealed class CorrectiveJoint
    {
        public string bonePath;
        public Quaternion referenceRotation = Quaternion.identity;
        public Vector3 axis = Vector3.right;
        public float startDegrees;
        public float fullDegrees = 100f;
        public bool magnitude;
    }

    [Serializable]
    public sealed class PoseCorrectiveBinding
    {
        public string rendererPath;
        public string shapeName;
        public CorrectiveJoint joint = new CorrectiveJoint();
        public CorrectiveJoint gate = new CorrectiveJoint();
    }

    [CreateAssetMenu(menuName = "A305/Characters/Pose Corrective Profile")]
    public sealed class PoseCorrectiveProfile : ScriptableObject
    {
        public List<PoseCorrectiveBinding> bindings = new List<PoseCorrectiveBinding>();
    }
}
