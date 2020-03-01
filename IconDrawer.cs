using System;
using UnityEditor;
using UnityEngine;

namespace AssetWatch
{
    [InitializeOnLoad]
    public class IconDrawer
    {
        private static float relativeMarkerScale = 0.15f;

        private static float minMarkerSize = 4f;

        private static float maxMarkerSize = 14f;

        private static Vector2 offset = new Vector2(0.1f, 0.1f);

        public static Texture UsageTexture { get; set; }

        public static float RelativeMarkerScale
        {
            get => relativeMarkerScale;
            set => relativeMarkerScale = Mathf.Max(value, 0.01f);
        }

        public static float MinMarkerSize
        {
            get => minMarkerSize;
            set => minMarkerSize = Mathf.Max(value, 1f);
        }

        public static float MaxMarkerSize
        {
            get => maxMarkerSize;
            set => maxMarkerSize = Mathf.Max(value, 1f);
        }

        public static Vector2 Offset
        {
            get => offset;
            set
            {
                offset = value;

                if (float.IsNaN(offset.x) || float.IsInfinity(offset.x))
                {
                    offset.x = 0f;
                }

                if (float.IsNaN(offset.y) || float.IsInfinity(offset.y))
                {
                    offset.y = 0f;
                }

                if (offset.x < 0f || offset.x > 1f)
                {
                    offset.x = Mathf.Clamp01(offset.x);
                }

                if (offset.y < 0f || offset.y > 1f)
                {
                    offset.y = Mathf.Clamp01(offset.y);
                }
            }
        }

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            UsageTexture = Resources.Load<Texture>("AssetWatchMarker");
            EditorApplication.projectWindowItemOnGUI += OverlayUsageIcon;
        }

        private static void OverlayUsageIcon(string guid, Rect selectionrect)
        {
            if (UsageTexture == null)
            {
                return;
            }

            var fileName = AssetDatabase.GUIDToAssetPath(guid);
            var prevColor = GUI.color;

            switch (AssetWatchDatabase.GetAssetUsage(fileName))
            {
                case UsageState.Unknown:
                    GUI.color = Color.blue;
                    break;
                case UsageState.Ignored:
                    return;
                case UsageState.Used:
                    GUI.color = Color.green;
                    break;
                case UsageState.Unused:
                    GUI.color = Color.red;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            var iconSize = Mathf.Min(selectionrect.width, selectionrect.height);
            var markerSize = Mathf.Max(Mathf.Min(iconSize * relativeMarkerScale, maxMarkerSize), minMarkerSize);
            var markerPosition = offset * iconSize;
            var rect = new Rect(selectionrect.min + markerPosition, Vector2.one * markerSize);

            GUI.DrawTexture(rect, UsageTexture);
            GUI.color = prevColor;
        }
    }
}
