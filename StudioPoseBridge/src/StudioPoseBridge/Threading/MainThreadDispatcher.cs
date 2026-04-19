using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using UnityEngine;
using StudioPoseBridge;

namespace StudioPoseBridge.Threading
{
    public sealed class MainThreadDispatcher : MonoBehaviour
    {
        private static readonly ConcurrentQueue<Action> Queue = new ConcurrentQueue<Action>();

        public static Task<T> RunAsync<T>(Func<T> job)
        {
            var tcs = new TaskCompletionSource<T>();
            Queue.Enqueue(() =>
            {
                try
                {
                    tcs.SetResult(job());
                }
                catch (Exception e)
                {
                    tcs.SetException(e);
                }
            });
            return tcs.Task;
        }

        public static Task RunAsync(Action job)
        {
            var tcs = new TaskCompletionSource<bool>();
            Queue.Enqueue(() =>
            {
                try
                {
                    job();
                    tcs.SetResult(true);
                }
                catch (Exception e)
                {
                    tcs.SetException(e);
                }
            });
            return tcs.Task;
        }

        private void Update()
        {
            while (Queue.TryDequeue(out var action))
            {
                try
                {
                    action();
                }
                catch (Exception e)
                {
                    StudioPoseBridgePlugin.Log.LogError(e);
                }
            }
        }
    }
}
