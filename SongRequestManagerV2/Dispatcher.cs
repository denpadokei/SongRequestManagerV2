using SongRequestManagerV2.Utils;
using System;
using System.Collections;

namespace SongRequestManagerV2
{
    public class Dispatcher
    {
        public static void RunCoroutine(IEnumerator enumerator)
        {
            MainThreadInvoker.Instance.Enqueue(enumerator);
        }

        public static void RunOnMainThread(Action action)
        {
            MainThreadInvoker.Instance.Enqueue(action);
        }

        public static void RunOnMainThread<T>(Action<T> action, T value)
        {
            MainThreadInvoker.Instance.Enqueue(() =>
            {
                try {
                    action?.Invoke(value);
                }
                catch (Exception e) {
                    Logger.Error(e);
                }
            });
        }
    }
}
