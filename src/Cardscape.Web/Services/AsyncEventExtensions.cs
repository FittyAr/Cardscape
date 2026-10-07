namespace Cardscape.Web.Services;

internal static class AsyncEventExtensions
{
    extension(Func<Task>? handlers)
    {
        /// <summary>
        /// Awaits each subscriber of an async event in turn, so a handler
        /// that re-renders sees the state the previous one left.
        /// </summary>
        public async Task InvokeSequentiallyAsync()
        {
            if (handlers is null)
            {
                return;
            }

            foreach (Func<Task> handler in handlers.GetInvocationList().Cast<Func<Task>>())
            {
                await handler();
            }
        }
    }
}
