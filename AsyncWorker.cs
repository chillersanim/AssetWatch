using System;
using System.Diagnostics;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace AssetWatch
{
    public abstract class AsyncWorker
    {
        public float MaxWorkTime = 0.01f;

        public bool IsWorking { get; private set; }

        public event Action WorkEnded;

        private readonly Stopwatch stopwatch = new Stopwatch();

        public bool Start()
        {
            if (IsWorking)
            {
                return false;
            }

            Initialize();
            stopwatch.Reset();
            IsWorking = true;
            EditorApplication.update += OnUpdate;
            return true;
        }

        public bool Stop()
        {
            if (!IsWorking)
            {
                return false;
            }

            IsWorking = false;
            EditorApplication.update -= OnUpdate;
            IsWorking = false;
            Finish();
            OnWorkEnded();
            return true;
        }

        protected abstract bool NextStep();

        protected virtual void Initialize()
        {
        }

        protected virtual void Finish()
        {
        }

        private void OnUpdate()
        {
            if (!IsWorking)
            {
                EditorApplication.update -= OnUpdate;
                return;
            }

            stopwatch.Restart();

            while (stopwatch.Elapsed.TotalSeconds < MaxWorkTime)
            {
                var result = false;

                try
                {
                    result = NextStep();
                }
                catch (Exception e)
                {
                    Debug.LogError("Work step failed, continue with next one...\n" + e);
                }

                if (!result)
                {
                    IsWorking = false;
                    EditorApplication.update -= OnUpdate;
                    stopwatch.Stop();
                    Finish();
                    OnWorkEnded();
                    return;
                }
            }

            stopwatch.Stop();
        }

        protected virtual void OnWorkEnded()
        {
            WorkEnded?.Invoke();
        }
    }
}
