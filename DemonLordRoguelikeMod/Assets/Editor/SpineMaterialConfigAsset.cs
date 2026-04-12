using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spine材质球配置数据 - 用于ScriptableObject存储
/// </summary>
[CreateAssetMenu(fileName = "SpineMaterialConfig", menuName = "Spine/材质球配置", order = 1)]
public class SpineMaterialConfigAsset : ScriptableObject
{
    [Serializable]
    public class DirectoryConfig
    {
        public string folderPath = "Assets";
        public bool enableStraightAlpha = false;
    }

    public List<DirectoryConfig> directoryConfigs = new List<DirectoryConfig>();
}
