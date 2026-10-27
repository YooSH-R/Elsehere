using System;
using System.IO;
using UnityEngine;

namespace A305.EditorTools.Tripo
{
    public sealed class TripoJobStore
    {
        private readonly string path;

        public TripoJobStore(string path)
        {
            this.path = Path.GetFullPath(path);
        }

        public void Save(TripoJob job)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporaryPath = path + ".tmp";
            File.WriteAllText(temporaryPath, JsonUtility.ToJson(job, true));
            if (File.Exists(path))
            {
                File.Replace(temporaryPath, path, null);
            }
            else
            {
                File.Move(temporaryPath, path);
            }
        }

        public TripoJob Load()
        {
            if (!File.Exists(path))
            {
                return null;
            }
            return JsonUtility.FromJson<TripoJob>(File.ReadAllText(path))
                ?? throw new InvalidOperationException("저장된 Tripo 작업을 읽을 수 없습니다.");
        }
    }
}
