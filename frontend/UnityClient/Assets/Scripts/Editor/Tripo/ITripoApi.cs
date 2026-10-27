using System.Threading;
using System.Threading.Tasks;

namespace A305.EditorTools.Tripo
{
    public interface ITripoApi
    {
        Task<string> UploadAsync(string imagePath, CancellationToken cancellationToken);
        Task<string> SubmitAsync(string endpoint, string json, CancellationToken cancellationToken);
        Task<TripoTask> QueryAsync(string taskId, CancellationToken cancellationToken);
        Task DownloadAsync(string url, string destination, CancellationToken cancellationToken);
    }
}
