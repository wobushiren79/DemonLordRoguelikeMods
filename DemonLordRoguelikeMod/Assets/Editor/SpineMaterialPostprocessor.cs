using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Spine材质球资源导入处理器 - 自动应用材质球设置
/// </summary>
public class SpineMaterialPostprocessor : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        // 加载配置
        var config = SpineMaterialConfig.Load();
        if (config == null || config.directoryConfigs.Count == 0)
            return;

        // 收集所有需要检查的资源路径
        var allChangedAssets = new List<string>();
        allChangedAssets.AddRange(importedAssets);
        allChangedAssets.AddRange(movedAssets);

        if (allChangedAssets.Count == 0)
            return;

        // 筛选出材质球资源
        var materialPaths = allChangedAssets
            .Where(path => Path.GetExtension(path).ToLower() == ".mat")
            .ToList();

        if (materialPaths.Count == 0)
            return;

        // 处理每个材质球
        foreach (var path in materialPaths)
        {
            ProcessMaterialIfNeeded(path, config);
        }
    }

    /// <summary>
    /// 检查并处理材质球
    /// </summary>
    private static void ProcessMaterialIfNeeded(string materialPath, SpineMaterialConfigAsset config)
    {
        // 检查是否在配置的目录中
        foreach (var dirConfig in config.directoryConfigs)
        {
            if (string.IsNullOrEmpty(dirConfig.folderPath))
                continue;

            // 检查材质球是否在配置的目录下
            if (!materialPath.StartsWith(dirConfig.folderPath))
                continue;

            // 加载材质球
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
                continue;

            // 检查是否是Spine Shader
            if (!IsSpineShader(material.shader))
                continue;

            // 应用材质球设置
            ApplyMaterialSettings(material, dirConfig);
            
            EditorUtility.SetDirty(material);
            Debug.Log($"[Spine材质球初始化] 已更新材质球: {materialPath}");
        }
    }

    /// <summary>
    /// 检查是否是Spine相关的Shader
    /// </summary>
    private static bool IsSpineShader(Shader shader)
    {
        if (shader == null)
            return false;

        string shaderName = shader.name.ToLower();
        return shaderName.Contains("spine");
    }

    /// <summary>
    /// 应用材质球设置
    /// </summary>
    private static void ApplyMaterialSettings(Material material, SpineMaterialConfigAsset.DirectoryConfig config)
    {
        // 设置 Straight Alpha Input 属性
        if (material.HasProperty("_StraightAlphaInput"))
        {
            float currentValue = material.GetFloat("_StraightAlphaInput");
            float targetValue = config.enableStraightAlpha ? 1f : 0f;
            
            if (Mathf.Abs(currentValue - targetValue) > 0.01f)
            {
                material.SetFloat("_StraightAlphaInput", targetValue);
                if (config.enableStraightAlpha)
                {
                    material.EnableKeyword("_STRAIGHT_ALPHA_INPUT");
                }
                else
                {
                    material.DisableKeyword("_STRAIGHT_ALPHA_INPUT");
                }
            }
        }
    }
}
