using System.Collections.Generic;
using System.Threading;
using System;
using UnityEngine;
using System.Collections;

namespace SongRequestManagerV2
{
    public class Dispatcher : MonoBehaviour
    {
        private static Dispatcher _instance;
        private static SynchronizationContext _currentContext;

        public static void RunAsync(Action action)
        {
            ThreadPool.QueueUserWorkItem(o => action());
        }

        public static void RunAsync(Action<object> action, object state)
        {
            ThreadPool.QueueUserWorkItem(o => action(o), state);
        }

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
                    Plugin.Logger.Error(e);
                }
            });
            //_currentContext?.Post(d =>
            //{
            //    action?.Invoke(value);
            //}, null);
        }

        public static void Initialize()
        {
            Plugin.Log("Start Initialize");
            if (_instance == null) {
                try {
                    _instance = new GameObject("Dispatcher").AddComponent<Dispatcher>();
                    DontDestroyOnLoad(_instance.gameObject);

                    _currentContext = SynchronizationContext.Current;
                }
                catch (Exception e) {
                    Plugin.Log($"{e}");
                }
            }
        }
    }
}
