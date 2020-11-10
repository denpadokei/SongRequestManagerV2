using System.Collections.Generic;
using System.Threading;
using System;
using UnityEngine;
using System.Collections;

namespace SongRequestManagerV2
{
    public class Dispatcher
    {
        public static void RunCoroutine(IEnumerator enumerator) => HMMainThreadDispatcher.instance.Enqueue(enumerator);

        public static void RunOnMainThread(Action action) => HMMainThreadDispatcher.instance.Enqueue(action);

        public static void RunOnMainThread<T>(Action<T> action, T value)
        {
            HMMainThreadDispatcher.instance.Enqueue(() =>
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
