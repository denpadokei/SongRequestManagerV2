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
        }

        public static void Initialize()
        {
            Plugin.Log("Start Initialize");
            if (_instance == null) {
                try {
                    _instance = new GameObject("Dispatcher").AddComponent<Dispatcher>();
                    DontDestroyOnLoad(_instance.gameObject);
                }
                catch (Exception e) {
                    Plugin.Log($"{e}");
                }
            }
        }
    }
}
