using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace AssetWatch
{
    [InitializeOnLoad]
    public static class WorkManager
    {
        private static readonly ConcurrentQueue<WorkEntry> WorkQueue = new ConcurrentQueue<WorkEntry>();

        private static Task runningTask;

        public static void EnqueueWork(WorkType type, Action action)
        {
            WorkQueue.Enqueue(new WorkEntry(type, action));
        }

        public static IWorkResult<T> EnqueueWork<T>(WorkType type, Func<T> function)
        {
            var workResult = new WorkResult<T>();
            var action = new Action(() =>
            {
                var result = function.Invoke();
                workResult.SetResult(result);
            });

            WorkQueue.Enqueue(new WorkEntry(type, action));

            return workResult;
        }

        public static void ClearQueuedWork()
        {
            while(WorkQueue.TryDequeue(out _)) { }
        }

        public static void CancelRunningWork()
        {
            if(runningTask != null && !runningTask.IsCompleted)
            {
                runningTask.Wait();
            }
        }

        public static void CancelAllWork()
        {
            ClearQueuedWork();
            CancelRunningWork();
        }

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            EditorApplication.update += OnUpdate;
        }

        private static void OnUpdate()
        {
            // If an async task is currently running
            if (runningTask != null)
            {
                // And hasn't finished
                if (!runningTask.IsCompleted)
                {
                    // Don't start the next task
                    return;
                }

                if(runningTask.IsFaulted)
                {
                    Debug.LogError("AssetWatch: Exception occured asynchronously:\n" + runningTask.Exception);
                }

                runningTask.Wait();
                runningTask = null;
                EditorApplication.RepaintProjectWindow();
            }

            // Otherwise try to get the next work entry
            if (!WorkQueue.TryDequeue(out var workEntry))
            {
                return;
            }

            if (workEntry.WorkType == WorkType.UnityLoop)
            {
                // The work entry needs to be run on the unity loop, thus we can't rely on the exception handling of tasks
                try
                {
                    workEntry.Action.Invoke();
                    EditorApplication.RepaintProjectWindow();
                }
                catch (Exception e)
                {
                    Debug.LogError("AssetWatch: Exception occured synchronously:\n" + e);
                }
            }
            else
            {
                EditorApplication.RepaintProjectWindow();
                var task = new Task(workEntry.Action);
                runningTask = task;
                task.Start(TaskScheduler.Default);
            }
        }

        private struct WorkEntry
        {
            public readonly WorkType WorkType;

            public readonly Action Action;

            public WorkEntry(WorkType workType, Action action)
            {
                this.WorkType = workType;
                this.Action = action;
            }
        }

        private class WorkResult<T> : IWorkResult<T>
        {
            private T result;

            private bool hasResult;

            public WorkResult()
            {
                hasResult = false;
            }

            public T GetResult()
            {
                if(!hasResult)
                {
                    throw new InvalidOperationException("Can't get result when the process hasn't been finished.");
                }

                return result;
            }

            public void SetResult(T value)
            {
                result = value;
                hasResult = true;
            }
        }
    }

    public enum WorkType
    {
        Async,

        UnityLoop
    }

    public interface IWorkResult<out T>
    {
        T GetResult();
    }
}
