using System.Collections.Generic;
using JetBrains.Annotations;

namespace AssetWatch
{
    public class AssetInfo : IDataContainer
    {
        private List<SceneInfo> scenesWithReference;

        public int Id { get; set; }

        public string Path { get; set; }

        public UsageState UsageState
        {
            get
            {
                if (IsUpdating)
                {
                    return UsageState.Unknown;
                }

                if(ForceIsUsed)
                {
                    return UsageState.Used;
                }

                if(scenesWithReference == null || scenesWithReference.Count == 0)
                {
                    return UsageState.Unused;
                }

                return UsageState.Used;
            }
        }

        public bool IsUpdating { get; set; }

        public bool ForceIsUsed { get; set; }

        [CanBeNull]
        public IEnumerable<SceneInfo> GetScenesWithReference()
        {
            return scenesWithReference;
        }

        public void AddReferencingScene(SceneInfo scene)
        {
            if (scenesWithReference == null)
            {
                scenesWithReference = new List<SceneInfo> {scene};
            }
            else
            {
                if (!scenesWithReference.Contains(scene))
                {
                    scenesWithReference.Add(scene);
                }
            }
        }

        public bool IsUsedBy(SceneInfo scene)
        {
            return scenesWithReference?.Contains(scene) ?? false;
        }

        public void RemoveReferencingScene(SceneInfo scene)
        {
            scenesWithReference?.Remove(scene);
        }

        public void RemoveFromAllScenes()
        {
            if (scenesWithReference == null)
            {
                return;
            }

            for (var i = scenesWithReference.Count - 1; i >= 0; i--)
            {
                scenesWithReference[i].MarkAssetUnused(this);
            }
        }
    }

    public enum UsageState
    {
        Ignored,

        Unknown,

        Used,

        Unused
    }
}
