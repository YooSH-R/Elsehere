using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace A305.EditorTools.Tripo
{
    // AssetDatabase 의존성 캐시가 갱신되기 전에도 저장된 메시 변경으로 승인을 만료시킨다.
    public static class TripoCharacterFingerprint
    {
        private static readonly Dictionary<string, (long time, long length, string hash)> Cache =
            new();

        public static string Of(UnityEngine.Object asset)
        {
            if (asset == null)
                return "missing";
            string path = AssetDatabase.GetAssetPath(asset);
            var signature = new StringBuilder(
                AssetDatabase.GetAssetDependencyHash(path).ToString()
            );
            foreach (
                string dependency in AssetDatabase
                    .GetDependencies(path, true)
                    .OrderBy(p => p, StringComparer.Ordinal)
            )
            {
                if (!File.Exists(dependency))
                    continue;
                var info = new FileInfo(dependency);
                long time = info.LastWriteTimeUtc.Ticks;
                if (
                    !Cache.TryGetValue(dependency, out var cached)
                    || cached.time != time
                    || cached.length != info.Length
                )
                {
                    using var sha = SHA256.Create();
                    using var stream = File.OpenRead(dependency);
                    cached = (time, info.Length, Convert.ToBase64String(sha.ComputeHash(stream)));
                    Cache[dependency] = cached;
                }
                signature.Append(dependency).Append(cached.hash);
                var loaded = AssetDatabase.LoadMainAssetAtPath(dependency);
                if (loaded != null && EditorUtility.IsDirty(loaded))
                    signature.Append(EditorJsonUtility.ToJson(loaded));
            }
            return Hash128.Compute(signature.ToString()).ToString();
        }
    }
}
