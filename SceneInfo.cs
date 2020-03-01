using System.Collections.Generic;

namespace AssetWatch
{
    public class SceneInfo : IDataContainer
    {
        private readonly List<AssetInfo> usedAssets = new List<AssetInfo>();

        private readonly HashSet<AssetInfo> infoCache = new HashSet<AssetInfo>();

        public int Id { get; set; }

        public string SceneName { get; set; }

        public string Path { get; set; }

        public IEnumerable<AssetInfo> UsedAssets() => usedAssets;

        public void MarkAssetUsed(AssetInfo info)
        {
            if (info == null)
            {
                return;
            }

            if(info.IsUsedBy(this))
            {
                return;
            }

            usedAssets.Add(info);
            info.AddReferencingScene(this);
        }

        public void MarkAssetsUsed(IList<AssetInfo> infos)
        {
            if(usedAssets.Capacity < usedAssets.Count + infos.Count)
            {
                usedAssets.Capacity = usedAssets.Count + infos.Count;
            }

            foreach (var info in infos)
            {
                MarkAssetUsed(info);
            }
        }

        public void MarkAssetUnused(AssetInfo info)
        {
            if(info == null)
            {
                return;
            }

            usedAssets.Remove(info);
            info.RemoveReferencingScene(this);
        }

        public void ClearUsedAssets()
        {
            foreach(var info in usedAssets)
            {
                info.RemoveReferencingScene(this);
            }

            usedAssets.Clear();
        }

        public void SetUsedAssets(IList<AssetInfo> infos)
        {
            // Remove no longer used
            foreach(var info in infos)
            {
                if(info == null)
                {
                    continue;
                }

                infoCache.Add(info);
            }

            for(var i = usedAssets.Count - 1; i >= 0; i--)
            {
                var used = usedAssets[i];

                if(!infoCache.Contains(used))
                {
                    usedAssets.RemoveAt(i);
                    used.RemoveReferencingScene(this);
                }
            }

            infoCache.Clear();

            // Add new
            foreach (var used in usedAssets)
            {
                infoCache.Add(used);
            }

            foreach(var info in infos)
            {
                if (info == null)
                {
                    continue;
                }

                if (!infoCache.Contains(info))
                {
                    usedAssets.Add(info);
                    info.AddReferencingScene(this);
                    infoCache.Add(info);
                }
            }

            infoCache.Clear();
        }
    }
}
