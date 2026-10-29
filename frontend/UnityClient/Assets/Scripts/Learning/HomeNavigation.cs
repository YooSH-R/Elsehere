using System;

namespace A305.Learning
{
    public static class HomeNavigation
    {
        public static bool TryReturn(
            string currentScene,
            bool isBusy,
            Func<string, string, bool> travel
        )
        {
            return currentScene != "HomeScene" && !isBusy && travel("HomeScene", null);
        }
    }
}
