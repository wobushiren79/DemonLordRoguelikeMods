using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Spine.Unity;

/// <summary>
/// 修复「多页 atlas 材质引用缺失」。
///
/// 症状（Unity 控制台）：
///   Material with texture name "5005_Secretary_2" not found for atlas asset: 5005_Secretary_Atlas
///   Spine.Unity.MaterialsTextureLoader:Load (Spine.AtlasPage, string)
///
/// 原因有两个叠加在一起：
///   1. atlas 的页数变化后，AtlasAsset.materials 没跟上（数量变少或是空的）；
///   2. spine-unity 的 AssetUtility.HasCustomMaterialsAssigned 只比较 materials[0] 的名字
///      是否等于「第一页应有的材质名」（单页 = &lt;primary&gt;_Material，多页 = &lt;primary&gt;_&lt;页名&gt;）。
///      不相等就判定为「用户自定义材质」，之后**永远不会再重建** materials 列表。
///      所以一旦页数增多而旧材质还是单页命名，就会永久卡在错误状态，反复重导入也没用。
///
/// 做法：对「页数 != 材质数」的 AtlasAsset，先清空 materials（解除“自定义”判定），
///   再重新导入它的 atlas 文本，交给 spine-unity 自己的 AssetUtility.IngestSpineAtlas 按页重建：
///   已存在的 &lt;primary&gt;_&lt;页名&gt;.mat 会被复用并重设贴图，缺的会按默认 shader 新建。
/// </summary>
public static class FixSpineAtlasMaterials
{
    /// <summary>扫描范围；只修 AeonsEcho 的话改成 "Assets/ModResource/Spine/AeonsEcho"</summary>
    private const string SearchRoot = "Assets/ModResource/Spine";

    [MenuItem("工具/Spine/修复多页 Atlas 材质引用", false, 51)]
    public static void FixAll()
    {
        string[] atlasGuids = AssetDatabase.FindAssets("t:SpineAtlasAsset", new[] { SearchRoot });
        var reimportPaths = new List<string>();
        int scanned = 0, broken = 0;

        foreach (string guid in atlasGuids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            SpineAtlasAsset atlasAsset = AssetDatabase.LoadAssetAtPath<SpineAtlasAsset>(assetPath);
            if (atlasAsset == null || atlasAsset.atlasFile == null)
                continue;
            scanned++;

            int pageCount = GetPageCount(atlasAsset);
            int materialCount = atlasAsset.materials != null ? atlasAsset.materials.Length : 0;
            if (pageCount <= 0 || pageCount == materialCount)
                continue;

            broken++;
            string atlasTxtPath = AssetDatabase.GetAssetPath(atlasAsset.atlasFile);
            Debug.LogWarning(string.Format("[Atlas材质修复] {0}: 页数 {1} != 材质数 {2} → 清空并重建（{3}）",
                atlasAsset.name, pageCount, materialCount, atlasTxtPath), atlasAsset);

            atlasAsset.materials = new Material[0];
            EditorUtility.SetDirty(atlasAsset);
            reimportPaths.Add(atlasTxtPath);
        }

        if (broken > 0)
        {
            AssetDatabase.SaveAssets();
            // 重新导入 atlas 文本 → 触发 spine-unity 的 IngestSpineAtlas 按页重建材质
            for (int i = 0; i < reimportPaths.Count; i++)
                AssetDatabase.ImportAsset(reimportPaths[i], ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();
        }

        Debug.Log(string.Format("[Atlas材质修复] 完成：扫描 {0} 个 AtlasAsset，修复 {1} 个（重新导入 {2} 个 atlas 文本）",
            scanned, broken, reimportPaths.Count));
    }

    /// <summary>只检查不修改，先把有问题的列出来</summary>
    [MenuItem("工具/Spine/仅检查 Atlas 材质引用", false, 52)]
    public static void CheckOnly()
    {
        string[] atlasGuids = AssetDatabase.FindAssets("t:SpineAtlasAsset", new[] { SearchRoot });
        int scanned = 0, broken = 0;
        foreach (string guid in atlasGuids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            SpineAtlasAsset atlasAsset = AssetDatabase.LoadAssetAtPath<SpineAtlasAsset>(assetPath);
            if (atlasAsset == null || atlasAsset.atlasFile == null)
                continue;
            scanned++;

            int pageCount = GetPageCount(atlasAsset);
            int materialCount = atlasAsset.materials != null ? atlasAsset.materials.Length : 0;
            if (pageCount <= 0 || pageCount == materialCount)
                continue;

            broken++;
            Debug.LogWarning(string.Format("[Atlas材质检查] {0}: 页数 {1} / 材质 {2}  ← {3}",
                atlasAsset.name, pageCount, materialCount, assetPath), atlasAsset);
        }
        Debug.Log(string.Format("[Atlas材质检查] 扫描 {0} 个 AtlasAsset，问题 {1} 个", scanned, broken));
    }

    /// <summary>读 atlas 文本里的页数（onlyMetaData=true 时用 NoOp 贴图加载器，不会报材质缺失）</summary>
    private static int GetPageCount(SpineAtlasAsset atlasAsset)
    {
        atlasAsset.Clear();
        var atlas = atlasAsset.GetAtlas(true);
        return atlas != null ? atlas.Pages.Count : 0;
    }
}
