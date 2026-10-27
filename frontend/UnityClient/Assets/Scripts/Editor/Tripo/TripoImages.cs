using System;
using System.IO;
using UnityEngine;

namespace A305.EditorTools.Tripo
{
    public sealed class TripoImages
    {
        private readonly string front;
        private readonly string left;
        private readonly string right;
        private readonly string back;

        public TripoImages(string front, string left, string right, string back)
        {
            this.front = front;
            this.left = left;
            this.right = right;
            this.back = back;
        }

        public void Validate()
        {
            foreach (string path in GetOrderedPaths())
            {
                if (string.IsNullOrWhiteSpace(path))
                    throw new ArgumentException("정면·좌측·우측·후면 이미지를 모두 선택하세요.");
            }
            foreach (string path in GetOrderedPaths())
            {
                string extension = Path.GetExtension(path).ToLowerInvariant();
                if (extension != ".png" && extension != ".jpg" && extension != ".jpeg")
                    throw new ArgumentException("PNG 또는 JPEG 이미지만 사용할 수 있습니다.");
                if (!File.Exists(path))
                    throw new ArgumentException(
                        "선택한 이미지 파일이 없습니다: " + Path.GetFileName(path)
                    );
                long bytes = new FileInfo(path).Length;
                if (bytes <= 0 || bytes > 20 * 1024 * 1024)
                    throw new ArgumentException(
                        "이미지는 비어 있지 않은 20MB 이하 파일이어야 합니다."
                    );
                byte[] contents = File.ReadAllBytes(path);
                bool png =
                    contents.Length >= 8
                    && contents[0] == 137
                    && contents[1] == 80
                    && contents[2] == 78
                    && contents[3] == 71;
                bool jpeg =
                    contents.Length >= 3
                    && contents[0] == 255
                    && contents[1] == 216
                    && contents[2] == 255;
                if (extension == ".png" ? !png : !jpeg)
                    throw new ArgumentException("이미지 형식과 파일 내용이 맞지 않습니다.");
                var texture = new Texture2D(2, 2);
                try
                {
                    if (!texture.LoadImage(contents, true))
                        throw new ArgumentException(
                            "이미지를 읽을 수 없습니다: " + Path.GetFileName(path)
                        );
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }
            }
        }

        public string[] GetOrderedPaths()
        {
            return new[] { front, left, back, right };
        }
    }
}
