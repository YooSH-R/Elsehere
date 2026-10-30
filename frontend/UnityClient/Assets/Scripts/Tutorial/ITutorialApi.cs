using System.Threading.Tasks;

namespace A305.Tutorial
{
    /// <summary>튜토리얼 완료(TUT-01)·건너뛰기(TUT-02). 실패하면 <see cref="A305.Api.ApiException"/>을 던진다.</summary>
    public interface ITutorialApi
    {
        Task CompleteAsync();

        Task SkipAsync();
    }
}
