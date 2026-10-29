using System;

namespace A305.EditorTools.Tripo
{
    public sealed class TripoRequestRejectedException : InvalidOperationException
    {
        public TripoRequestRejectedException(int code, string guidance)
            : base($"Tripo 오류 {code}: {guidance}") { }
    }
}
