﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.VersionControl;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace AssetWatch
{
    [InitializeOnLoad]
    public class AssetWatchDatabase
    {
        /// <summary>
        /// Don't track files that end with the given text.
        /// </summary>
        private static readonly string[] ExcludedFileExtensions = {".meta", ".asset"};

        /// <summary>
        /// Files that end with the given text will always be marked as used.
        /// </summary>
        private static readonly string[] ForcedIsUsedFileExtensions = {".cs", ".unity", ".asmdef", ".dotsettings"};

        /// <summary>
        /// Contains all tracked assets using the path as key and the asset info as value.
        /// </summary>
        private static readonly Dictionary<string, AssetInfo> AssetInfos = new Dictionary<string, AssetInfo>();

        /// <summary>
        /// Contains all tracked scenes using the path as key and the scene info as value.
        /// </summary>
        private static readonly Dictionary<string, SceneInfo> SceneInfos = new Dictionary<string, SceneInfo>();

        private static readonly object LockObject = new object();

        public static UsageState GetAssetUsage(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return UsageState.Ignored;
            }

            lock (LockObject)
            {
                if (AssetInfos.TryGetValue(path, out var assetInfo))
                {
                    return assetInfo.UsageState;
                }

                return UsageState.Ignored;
            }
        }

        internal static void AddNewAssets(IEnumerable<string> paths)
        {
            lock (LockObject)
            {
                foreach (var path in paths)
                {
                    if (AssetInfos.ContainsKey(path))
                    {
                        continue;
                    }

                    // Assume new assets aren't being referenced by any scene yet, so just add it to the database
                    ImportAsset(path);

                    if (path.EndsWith(".unity", StringComparison.InvariantCultureIgnoreCase))
                    {
                        UpdateScene(path);
                    }
                }
            }

            EditorApplication.RepaintProjectWindow();
        }

        internal static void RemoveAssets(IEnumerable<string> paths)
        {
            lock (LockObject)
            {
                foreach (var path in paths)
                {
                    if (AssetInfos.TryGetValue(path, out var info))
                    {
                        info.RemoveFromAllScenes();
                        AssetInfos.Remove(path);
                    }

                    if (path.EndsWith(".unity", StringComparison.InvariantCultureIgnoreCase))
                    {
                        if (!SceneInfos.TryGetValue(path, out var sceneInfo))
                        {
                            continue;
                        }

                        sceneInfo.ClearUsedAssets();
                        SceneInfos.Remove(path);
                    }
                }
            }

            EditorApplication.RepaintProjectWindow();
        }

        internal static void MoveAsset(string oldPath, string newPath)
        {
            lock (LockObject)
            {
                if (!AssetInfos.TryGetValue(oldPath, out var assetInfo))
                {
                    return;
                }

                AssetInfos.Remove(oldPath);

                if (!AssetInfos.ContainsKey(newPath))
                {
                    assetInfo.Path = newPath;
                    AssetInfos.Add(newPath, assetInfo);
                }

                // If the moved asset is a scene, update the scene reference as well
                if (oldPath.EndsWith(".unity", StringComparison.InvariantCultureIgnoreCase))
                {
                    if (SceneInfos.TryGetValue(oldPath, out var sceneInfo))
                    {
                        SceneInfos.Remove(oldPath);

                        if (!SceneInfos.ContainsKey(newPath))
                        {
                            sceneInfo.Path = newPath;
                            SceneInfos.Add(newPath, sceneInfo);
                        }
                    }
                }
            }

            EditorApplication.RepaintProjectWindow();
        }

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            EditorSceneManager.sceneSaved += OnSceneSaved;
            UpdateAllData(); 
        }

        private static void OnSceneSaved(Scene scene)
        {
            var path = scene.path;
            UpdateFromScene(path);
        }

        private static void UpdateFromScene(string sceneName)
        {
            SceneInfo sceneInfo;

            lock (LockObject)
            {
                SceneInfos.TryGetValue(sceneName, out sceneInfo);
            }

            if (sceneInfo == null)
            {
                return;
            }

            WorkManager.EnqueueWork(WorkType.Async, () =>
            {
                foreach (var assetInfo in AssetInfos.Values)
                {
                    assetInfo.IsUpdating = true;
                }
            });

            WorkManager.EnqueueWork(WorkType.Async, () => UpdateScene(sceneName));
            WorkManager.EnqueueWork(WorkType.Async, PostUpdate);
        }

        [MenuItem("Assets/Usage/Refresh all")]
        private static void UpdateAllData()
        {
            var assetsPath = Application.dataPath;

            WorkManager.CancelAllWork();

            // Clear old data
            WorkManager.EnqueueWork(WorkType.UnityLoop, () =>
            {
                lock (LockObject)
                {
                    AssetInfos.Clear();
                    SceneInfos.Clear();
                }
            });

            // Collect all assets
            WorkManager.EnqueueWork(WorkType.Async, () => FindAssetsJob(assetsPath));
            
            // Collect scene dependencies
            var sceneDependencies = new Dictionary<string, string[]>();

            WorkManager.EnqueueWork(WorkType.UnityLoop, () =>
            {
                var total = SceneInfos.Values.Count;
                var progress = 0f;

                foreach (var sceneInfo in SceneInfos.Values)
                {
                    EditorUtility.DisplayProgressBar("AssetWatch: Collecting scene dependencies", sceneInfo.Path, progress);
                    progress += 1f / total;

                    var dependencyResult = AssetDatabase.GetDependencies(sceneInfo.Path, true);
                    sceneDependencies.Add(sceneInfo.Path, dependencyResult);
                }

                EditorUtility.ClearProgressBar();
            });

            // Update all scene dependencies
            WorkManager.EnqueueWork(WorkType.Async, () =>
            {
                foreach (var sceneInfo in SceneInfos.Values)
                {
                    if (sceneDependencies.TryGetValue(sceneInfo.Path, out var dependencies))
                    {
                        UpdateSceneDependencies(sceneInfo.Path, dependencies);
                    }
                    else
                    {
                        Debug.LogWarning("AssetWatch: Internal data representation out of sync, please use \"Assets/Usage/Refresh All\" to ensure correct behavior.");
                    }
                }
            });

            WorkManager.EnqueueWork(WorkType.Async, PostUpdate);
        }

        private static void FindAssetsJob(string root)
        {
            AssetInfos.Clear();

            var fileEnumerator = Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories);

            foreach (var file in fileEnumerator)
            {
                var assetName = "Assets" + file.Substring(root.Length).Replace('\\', '/').Replace("//", "/");
                var info = ImportAsset(assetName);

                if (info != null)
                {
                    info.IsUpdating = true;
                }
            }
        }

        private static AssetInfo ImportAsset(string assetName)
        {
            if (TestFile(assetName))
            {
                var assetInfo = new AssetInfo {Path = assetName};

                foreach(var enforced in ForcedIsUsedFileExtensions)
                {
                    if (assetName.EndsWith(enforced, StringComparison.InvariantCultureIgnoreCase))
                    {
                        assetInfo.ForceIsUsed = true;
                        break;
                    }
                }

                lock (LockObject)
                {
                    if (!AssetInfos.ContainsKey(assetName))
                    {
                        AssetInfos.Add(assetName, assetInfo);
                    }
                }

                if (assetName.EndsWith(".unity", StringComparison.InvariantCultureIgnoreCase))
                {
                    var sceneName = Path.GetFileNameWithoutExtension(assetName);
                    var sceneInfo = new SceneInfo{Path = assetName, SceneName = sceneName };

                    lock (LockObject)
                    {
                        if (!SceneInfos.ContainsKey(assetName))
                        {
                            SceneInfos.Add(assetName, sceneInfo);
                        }
                    }
                }

                return assetInfo;
            }

            return null;
        }

        private static void UpdateScene(string path)
        {
            // Get dependencies from unity
            var dependencyResult =
                WorkManager.EnqueueWork(WorkType.UnityLoop, () =>
                {
                    Debug.Log($"AssetWatch: ");

                    var stopwatch = new Stopwatch();
                    stopwatch.Start();

                    var dependencies = AssetDatabase.GetDependencies(path, true);

                    stopwatch.Stop();
                    Debug.Log($"AssetWatch: Started scanning scene: {path}, Find dependencies took {stopwatch.Elapsed.TotalMilliseconds} ms, Dependencies found: {dependencies.Length}");

                    return dependencies;
                });

            WorkManager.EnqueueWork(WorkType.Async, () => UpdateSceneDependencies(path, dependencyResult.GetResult()));
        }

        private static void UpdateSceneDependencies(string path, string[] dependencies)
        {
            SceneInfo info;

            lock(LockObject)
            { 
                if (!SceneInfos.TryGetValue(path, out info))
                {
                    Debug.LogError("AssetWatch: Scene is not tracked.");
                    return;
                }
            }

            var usedAssets = new AssetInfo[dependencies.Length];

            lock (LockObject)
            {
                for (var i = 0; i < dependencies.Length; i++)
                {
                    if (AssetInfos.TryGetValue(dependencies[i], out var assetInfo))
                    {
                        usedAssets[i] = assetInfo;
                    }
                    else
                    {
                        usedAssets[i] = null;
                    }
                }
            }

            info.SetUsedAssets(usedAssets);
        }
         
        private static void PostUpdate()
        {
            foreach(var assetInfo in AssetInfos.Values)
            {
                assetInfo.IsUpdating = false;
            }
        }

        private static bool TestFile(string filePath)
        {
            var fileName = Path.GetFileName(filePath);

            if (string.IsNullOrWhiteSpace(fileName))
            {
                return false;
            }

            fileName = fileName.Trim();

            var dotIndex = fileName.LastIndexOf('.');
            if (dotIndex < 0 || dotIndex >= fileName.Length - 1)
            {
                return false;
            }

            foreach (var excluded in ExcludedFileExtensions)
            {
                if (fileName.EndsWith(excluded, StringComparison.InvariantCultureIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
 