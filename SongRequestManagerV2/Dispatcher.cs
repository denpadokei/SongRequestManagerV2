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

        public static void RunCoroutine(IEnumerator enumerator)
        {
            if (!_instance) {
                return;
            }

            _instance.StartCoroutine(enumerator);
        }

        public static void RunOnMainThread(Action action)
        {
            _currentContext?.Post(d =>
            {
                action?.Invoke();
            }, null);
        }

        public static void RunOnMainThread<T>(Action<T> action, T value)
        {
            _currentContext?.Post(d =>
            {
                action?.Invoke(value);
            }, null);
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
