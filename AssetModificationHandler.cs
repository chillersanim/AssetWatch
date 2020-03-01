using System;
using System.Collections.Generic;
using UnityEditor;

namespace AssetWatch
{
    [InitializeOnLoad]
    public class AssetModificationHandler : UnityEditor.AssetModificationProcessor
    {
        private const int UpdateDelay = 5;

        private static readonly List<string> AddedAssets = new List<string>();

        private static readonly List<string> RemovedAssets = new List<string>();

        private static int updateDelay;

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            EditorApplication.update += OnUpdate;
        }

        private static void OnUpdate()
        {
            if (updateDelay > 0)
            {
                updateDelay--;
                return;
            }

            if (AddedAssets.Count > 0)
            {
                AssetWatchDatabase.AddNewAssets(AddedAssets);
                AddedAssets.Clear();
            }

            if (RemovedAssets.Count > 0)
            {
                AssetWatchDatabase.RemoveAssets(RemovedAssets);
                RemovedAssets.Clear();
            }
        }

        private static void OnWillCreateAsset(string assetName)
        {
            RemoveMetaEnding(ref assetName);

            if (RemovedAssets.Contains(assetName))
            {
                RemovedAssets.Remove(assetName);
            }

            if (!AddedAssets.Contains(assetName))
            {
                AddedAssets.Add(assetName);
                updateDelay = UpdateDelay;
            }
        }

        private static AssetDeleteResult OnWillDeleteAsset(string assetName, RemoveAssetOptions options)
        {
            RemoveMetaEnding(ref assetName);

            if (AddedAssets.Contains(assetName))
            {
                AddedAssets.Remove(assetName);
            }

            if (!RemovedAssets.Contains(assetName))
            {
                RemovedAssets.Add(assetName);
                updateDelay = UpdateDelay;
            }

            return AssetDeleteResult.DidNotDelete;
        }

        private static AssetMoveResult OnWillMoveAsset(string sourcePath, string destinationPath)
        {
            RemoveMetaEnding(ref sourcePath);
            RemoveMetaEnding(ref destinationPath);

            AssetWatchDatabase.MoveAsset(sourcePath, destinationPath);
            return AssetMoveResult.DidNotMove;
        }

        private static void RemoveMetaEnding(ref string text)
        {
            if (text.EndsWith(".meta", StringComparison.InvariantCultureIgnoreCase))
            {
                text = text.Substring(0, text.Length - 5);
            }
        }
    }
}
