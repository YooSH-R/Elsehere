using System;
using System.Threading;
using System.Threading.Tasks;

namespace A305.EditorTools.Tripo
{
    // 업로드 함수만 받아 생성·리깅·조회에 접근할 수 없도록 한다.
    public static class TripoUploadProbe
    {
        public static async Task RunAsync(
            string[] paths,
            Func<string, CancellationToken, Task<string>> upload,
            Action<int> completed,
            CancellationToken cancellation
        )
        {
            for (int i = 0; i < paths.Length; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                await upload(paths[i], cancellation);
                cancellation.ThrowIfCancellationRequested();
                completed(i + 1);
            }
        }
    }
}
