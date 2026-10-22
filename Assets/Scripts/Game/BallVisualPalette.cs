using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BallVisualPalette", menuName = "Tube Sort/Ball Visual Palette")]
public class BallVisualPalette : ScriptableObject
{
    [Serializable]
    public class BallVisualEntry
    {
        [Tooltip("这条配置对应的小球颜色枚举。")]
        public BallColorType color = BallColorType.White;
        [Tooltip("该颜色小球使用的主体 Sprite；为空时保留预制体上的默认 Sprite。")]
        public Sprite sprite;
        public Sprite lazhuSprite;
        [Tooltip("长试管合成时使用的蜡烛 Sprite。")]
        public Sprite longLazhuSprite;
        [Tooltip("该颜色小球选中/移动时使用的发光颜色。")]
        public Color tint = Color.white;
    }

    [Tooltip("所有小球颜色对应的视觉资源配置。")]
    [SerializeField] private List<BallVisualEntry> entries = new List<BallVisualEntry>();

    public bool TryGetVisual(BallColorType color, out BallVisualEntry entry)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].color == color)
            {
                entry = entries[i];
                return true;
            }
        }

        entry = null;
        return false;
    }
}
