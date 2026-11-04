using System;
using UnityEngine;

namespace A305.World
{
    /// <summary>씬을 바꾸지 않는 상가도 목적지로 쓸 수 있도록 정차·하차 지점을 함께 보관한다.</summary>
    [Serializable]
    public sealed class TaxiDestination
    {
        [SerializeField]
        private string code;

        [SerializeField]
        private string displayName;

        [SerializeField]
        private Vector3 stop;

        [SerializeField]
        private Vector3 alighting;

        public string Code => code;
        public string DisplayName => displayName;
        public Vector3 Stop => stop;
        public Vector3 Alighting => alighting;
    }
}
