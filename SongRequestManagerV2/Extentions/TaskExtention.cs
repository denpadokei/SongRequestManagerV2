using System;
using System.Threading.Tasks;

namespace SongRequestManagerV2.Extentions
{
    public static class TaskExtention
    {
        public static async void Await(this Task task, Action callback, Action<Exception> error = null, Action final = null, bool isConfigurAwait = false)
        {
            try {
                await task.ConfigureAwait(isConfigurAwait);
                callback?.Invoke();
            }
            catch (Exception e) {
                error?.Invoke(e);
            }
            finally {
                final?.Invoke();
            }
        }
        public static async void Await<T>(this Task<T> task, Action<T> callback, Action<Exception> error = null, Action final = null, bool isConfigurAwait = false)
        {
            try {
                var result = await task.ConfigureAwait(isConfigurAwait);
                callback?.Invoke(result);
            }
            catch (Exception e) {
                error?.Invoke(e);
            }
            finally {
                final?.Invoke();
            }
        }
    }
}
