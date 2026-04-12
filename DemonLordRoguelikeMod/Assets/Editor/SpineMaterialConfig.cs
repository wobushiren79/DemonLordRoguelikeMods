using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Spine材质球配置管理类
/// </summary>
public static class SpineMaterialConfig
{
    private const string CONFIG_PATH = "Assets/Editor/SpineMaterialConfig.asset";

    /// <summary>
    /// 加载配置
    /// </summary>
    public static SpineMaterialConfigAsset Load()
    {
        var config = AssetDatabase.LoadAssetAtPath<SpineMaterialConfigAsset>(CONFIG_PATH);
        if (config == null)
        {
            // 创建默认配置
            config = ScriptableObject.CreateInstance<SpineMaterialConfigAsset>();
            
            // 确保目录存在
            string directory = Path.GetDirectoryName(CONFIG_PATH);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            AssetDatabase.CreateAsset(config, CONFIG_PATH);
            AssetDatabase.SaveAssets();
        }
        return config;
    }

    /// <summary>
    /// 保存配置
    /// </summary>
    public static void Save(SpineMaterialConfigAsset config)
    {
        if (config != null)
        {
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
        }
    }

    /// <summary>
    /// 手动触发指定目录的材质球初始化
    /// </summary>
    public static void InitializeMaterialsInFolder(string folderPath, bool enableStraightAlpha)
    {
        if (!Directory.Exists(folderPath))
        {
            Debug.LogWarning($"[Spine材质球初始化] 目录不存在: {folderPath}");
            return;
        }

        // 查找目录下的所有材质球
        string[] materialGuids = AssetDatabase.FindAssets("t:Material", new[] { folderPath });
        int processedCount = 0;

        foreach (string guid in materialGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            
            if (material == null || material.shader == null)
                continue;

            // 只处理Spine相关的Shader
            string shaderName = material.shader.name.ToLower();
            if (!shaderName.Contains("spine/"))
                continue;

            // 应用设置
            if (material.HasProperty("_StraightAlphaInput"))
            {
                float targetValue = enableStraightAlpha ? 1f : 0f;
                material.SetFloat("_StraightAlphaInput", targetValue);
                
                if (enableStraightAlpha)
                {
                    material.EnableKeyword("_STRAIGHT_ALPHA_INPUT");
                }
                else
                {
                    material.DisableKeyword("_STRAIGHT_ALPHA_INPUT");
                }
                
                EditorUtility.SetDirty(material);
                processedCount++;
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[Spine材质球初始化] 已处理 {processedCount} 个材质球，目录: {folderPath}");
    }

    /// <summary>
    /// 初始化所有配置目录的材质球
    /// </summary>
    public static void InitializeAllMaterials(SpineMaterialConfigAsset config)
    {
        if (config == null || config.directoryConfigs.Count == 0)
        {
            Debug.Log("[Spine材质球初始化] 没有配置任何目录");
            return;
        }

        int totalProcessed = 0;
        foreach (var dirConfig in config.directoryConfigs)
        {
            if (string.IsNullOrEmpty(dirConfig.folderPath))
                continue;

            int countBefore = totalProcessed;
            InitializeMaterialsInFolder(dirConfig.folderPath, dirConfig.enableStraightAlpha);
        }

        Debug.Log($"[Spine材质球初始化] 所有目录处理完成");
    }
}
