using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace A305.Theme.Tests
{
    /// <summary>
    /// 웹 빌드의 텍스처 압축(DXT)은 4픽셀 블록 단위라, 가로·세로가 4의 배수가 아닌 UI 그림은 압축 설정이 켜져 있어도
    /// 원본(픽셀당 4바이트)으로 들어간다. 휴대폰의 첫 로딩과 메모리가 이 크기에 비례하므로 UI 그림은 4의 배수로 둔다.
    /// 맞지 않는 그림은 Tools → A305 → UI → Pad UI Images To Multiple Of 4로 오른쪽·위를 투명하게 덧댄다.
    /// </summary>
    public class UiImageSizeTests
    {
        [Test]
        public void UiImages_HaveSidesThatAreMultiplesOfFour()
        {
            string uiFolder = Path.Combine(Application.dataPath, "Art", "UI");
            string[] offenders = Directory
                .GetFiles(uiFolder, "*.png", SearchOption.AllDirectories)
                .Where(path =>
                {
                    (int width, int height) = PngSize(path);
                    return width % 4 != 0 || height % 4 != 0;
                })
                .Select(path => path.Substring(uiFolder.Length + 1).Replace('\\', '/'))
                .OrderBy(path => path)
                .ToArray();

            Assert.That(
                offenders,
                Is.Empty,
                "Tools → A305 → UI → Pad UI Images To Multiple Of 4로 맞춰 주세요."
            );
        }

        // PNG 머리(IHDR)의 가로·세로. Unity로 가져오지 않고 파일만 읽는다.
        private static (int width, int height) PngSize(string path)
        {
            using var stream = File.OpenRead(path);
            var header = new byte[24];
            stream.Read(header, 0, header.Length);
            return (BigEndian(header, 16), BigEndian(header, 20));
        }

        private static int BigEndian(byte[] bytes, int offset) =>
            bytes[offset] << 24
            | bytes[offset + 1] << 16
            | bytes[offset + 2] << 8
            | bytes[offset + 3];
    }
}
