using System.Threading.Tasks;
using A305.Api;

namespace A305.Tutorial
{
    /// <summary>TUT-01 <c>POST /api/v1/me/tutorial/complete</c>, TUT-02 <c>POST /api/v1/me/tutorial/skip</c>.</summary>
    public sealed class TutorialApi : ITutorialApi
    {
        private readonly ApiClient client;

        public TutorialApi(ApiClient client)
        {
            this.client = client;
        }

        public Task CompleteAsync()
        {
            return client.PostAsync("/api/v1/me/tutorial/complete", null);
        }

        public Task CompleteBenDialogueAsync()
        {
            return client.PostAsync("/api/v1/me/tutorial/ben-dialogue/complete", null);
        }

        public Task SkipAsync()
        {
            return client.PostAsync("/api/v1/me/tutorial/skip", null);
        }
    }
}
