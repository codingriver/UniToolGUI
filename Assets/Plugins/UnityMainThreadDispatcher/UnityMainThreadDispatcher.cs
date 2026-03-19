using System;
using System.Collections.Generic;
using UnityEngine;


    /// <summary>
    /// 将工作线程的回调 marshal 到 Unity 主线程执行。
    /// 挂载到场景中任意常驻 GameObject 上。
    /// </summary>
    public class UnityMainThreadDispatcher : MonoBehaviour
    {
        private static readonly Queue<Action> _queue = new Queue<Action>();
        private static UnityMainThreadDispatcher _instance;

        public static void Enqueue(Action action)
        {
            if (action == null) return;
            lock (_queue) _queue.Enqueue(action);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            while (true)
            {
                if (_queue.Count == 0) break;
                Action action;
                lock (_queue)
                {
                    action = _queue.Dequeue();
                }
                try { action(); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }
    }

