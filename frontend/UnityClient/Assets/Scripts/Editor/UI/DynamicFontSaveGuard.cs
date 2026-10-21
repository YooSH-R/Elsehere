using System.Linq;
using UnityEditor;
using UnityEngine;

namespace A305.EditorTools.UI
{
    /// <summary>
    /// 동적 글꼴 에셋은 Editor에서 글자가 화면에 나올 때마다 아틀라스에 글자를 채우고 바뀐 것으로 표시된다.
    /// 그대로 저장하면 Editor를 쓴 사람마다 글꼴 에셋이 수만 줄씩 바뀌어 커밋이 커지고 충돌이 생기므로 저장에서 뺀다.
    /// 채운 글자는 다음 실행에서 필요할 때 다시 채워지고, 빌드에서도 비운 채로 시작한다.
    /// 글꼴 설정은 <see cref="UiFontBuilder"/>로만 바꾼다.
    /// </summary>
    public class DynamicFontSaveGuard : AssetModificationProcessor
    {
        private static bool hasNotified;

        private static string[] OnWillSaveAssets(string[] paths)
        {
            if (UiFontBuilder.IsSavingFontSettings)
            {
                return paths;
            }

            string[] pathsToSave = paths
                .Where(path => !UiFontBuilder.DynamicFontAssetPaths.Contains(path))
                .ToArray();
            if (pathsToSave.Length != paths.Length && !hasNotified)
            {
                hasNotified = true;
                Debug.Log(
                    "Pretendard 동적 글꼴 에셋에 채워진 글자는 저장하지 않습니다. 글꼴 설정은 Tools/A305/UI/Set Up Fonts로 바꿉니다."
                );
            }

            return pathsToSave;
        }
    }
}
