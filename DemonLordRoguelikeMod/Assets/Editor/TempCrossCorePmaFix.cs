using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 【一次性临时脚本，跑完即删】CrossCoreSpine 资源接入的 PMA 修正。
/// <para>背景：Spine/Skeleton shader 固定 PMA 混合（Blend One OneMinusSrcAlpha），直通 alpha 贴图/开了直通开关的材质
/// 进 bundle 后 shader 只带 PMA 变体 → 主工程运行时白边（BrownDust 2026-09-29 事故，详见主项目记忆 project_spine_mod_pma_requirement）。</para>
/// <para>CrossCore 现状（2026-09-29 全量校验）：295 张本体图集 PNG 仅 2 张直通（70400_skin_LycorisRadiata03c.png、
/// CG0040_WholeFamilyHaveFun.png，透明区非零 RGB 占比 5~6%）；全部 511 个材质 _StraightAlphaInput=1（直通开关全开）。</para>
/// <para>本脚本做两件事：① 源目录全部 PNG 抽样检测直通（alpha=0 像素 RGB 非零占比 &gt;5%）→ rgb*=a 转 PMA（幂等，
/// 已是 PMA 的图不受影响）并强制重导；② 全部非特效层材质关直通开关（_StraightAlphaInput=0 + DisableKeyword）。</para>
/// <para>跑完确认日志无误后删除本文件（与 GirlWars/BrownDust 的 PMA 转换脚本同生命周期）。</para>
/// </summary>
public static class TempCrossCorePmaFix
{
    /// <summary>Spine 资源源目录</summary>
    private const string SourceFolder = "Assets/ModResource/Spine/CrossCore";
    /// <summary>直通判定阈值：透明区非零 RGB 像素占比超过该值视为直通贴图（噪点级 5% 以下不处理）</summary>
    private const float StraightRatioThreshold = 0.05f;
    /// <summary>每张 PNG 的抽样像素数</summary>
    private const int SampleCount = 4000;

    /// <summary>
    /// 一键修正：PNG 直通转 PMA + 材质关直通开关
    /// </summary>
    [MenuItem("工具/Mod/CrossCoreSpine/[一次性]PMA修正(PNG转PMA+材质关直通)", false, 300)]
    public static void FixAll()
    {
        int pngConverted = ConvertStraightPngsToPma();
        int matFixed = DisableMaterialStraightAlpha();
        AssetDatabase.SaveAssets();
        Debug.Log($"[CrossCoreSpine] PMA 修正完成：PNG 转 PMA {pngConverted} 张、材质关直通 {matFixed} 个。\n跑完确认无误后请删除 TempCrossCorePmaFix.cs（一次性脚本）");
        EditorUtility.RevealInFinder(Path.GetFullPath(SourceFolder));
    }

    #region PNG 直通转 PMA

    /// <summary>
    /// 特效/背景层判定（与 gen_crosscore_spine_mod.py 的 EFFECT_LAYER_PATTERN / CrossCoreSpineModBuilder.IsEffectLayer 同规则）：
    /// 名字中 effect 前有分隔符（_、-、空格）即视为特效层。PNG 不按此过滤（图集命名与 SkeletonData 未必同名），全部转换幂等无害
    /// </summary>
    private static bool IsEffectLayer(string assetName)
    {
        return System.Text.RegularExpressions.Regex.IsMatch(assetName, @"[_\-\s]effect[_\-\s]?", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// 遍历源目录全部 PNG，抽样检测直通 alpha（透明区 RGB 非零占比超阈值）→ rgb*=a 转 PMA 写回并强制重导
    /// </summary>
    /// <returns>实际转换的 PNG 数</returns>
    private static int ConvertStraightPngsToPma()
    {
        var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { SourceFolder });
        int converted = 0, checkedCount = 0;
        foreach (var guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (!assetPath.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
                continue;
            checkedCount++;
            string fullPath = Path.GetFullPath(assetPath);
            // 文件级读写解码，不依赖导入器的 isReadable 设置
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(File.ReadAllBytes(fullPath)))
            {
                Object.DestroyImmediate(tex);
                continue;
            }
            var pixels = tex.GetPixels32();
            // 抽样检测：alpha=0 且 RGB 非零的像素占比
            int transparent = 0, nonZero = 0;
            var rng = new System.Random(42);
            for (int i = 0; i < SampleCount; i++)
            {
                var c = pixels[rng.Next(pixels.Length)];
                if (c.a == 0)
                {
                    transparent++;
                    if (c.r != 0 || c.g != 0 || c.b != 0)
                        nonZero++;
                }
            }
            bool isStraight = transparent > 0 && (float)nonZero / transparent > StraightRatioThreshold;
            if (!isStraight)
            {
                Object.DestroyImmediate(tex);
                continue;
            }
            // rgb*=a 转 PMA（对透明区非零像素归零，半透明区按 alpha 衰减；已是 PMA 的区域不受影响，幂等）
            for (int i = 0; i < pixels.Length; i++)
            {
                var c = pixels[i];
                c.r = (byte)Mathf.RoundToInt(c.r * c.a / 255f);
                c.g = (byte)Mathf.RoundToInt(c.g * c.a / 255f);
                c.b = (byte)Mathf.RoundToInt(c.b * c.a / 255f);
                pixels[i] = c;
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            File.WriteAllBytes(fullPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            // 强制同步重导（绕过 Library artifact 缓存，防 NikkeSpine 2026-09-29 的导入 artifact 损坏类问题）
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            converted++;
            Debug.Log($"[CrossCoreSpine] PNG 直通转 PMA：{assetPath}（透明区非零RGB占比 {nonZero}/{transparent}）");
        }
        Debug.Log($"[CrossCoreSpine] PNG 检查 {checkedCount} 张，直通转换 {converted} 张");
        return converted;
    }

    #endregion

    #region 材质关直通开关

    /// <summary>
    /// 全部非特效层材质关直通开关：_StraightAlphaInput=0 + DisableKeyword(_STRAIGHT_ALPHA_INPUT)。
    /// 贴图已是 PMA，材质直通开关只在编辑器内多乘一次 alpha（边缘偏暗），bundle 运行时无影响——关掉与规范对齐
    /// </summary>
    /// <returns>实际修改的材质数</returns>
    private static int DisableMaterialStraightAlpha()
    {
        var guids = AssetDatabase.FindAssets("t:Material", new[] { SourceFolder });
        int count = 0;
        foreach (var guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (IsEffectLayer(Path.GetFileNameWithoutExtension(assetPath)))
                continue;
            var mat = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (mat == null || !mat.HasFloat("_StraightAlphaInput"))
                continue;
            bool dirty = false;
            if (!Mathf.Approximately(mat.GetFloat("_StraightAlphaInput"), 0f))
            {
                mat.SetFloat("_StraightAlphaInput", 0f);
                dirty = true;
            }
            if (mat.IsKeywordEnabled("_STRAIGHT_ALPHA_INPUT"))
            {
                mat.DisableKeyword("_STRAIGHT_ALPHA_INPUT");
                dirty = true;
            }
            if (dirty)
            {
                EditorUtility.SetDirty(mat);
                count++;
            }
        }
        return count;
    }

    #endregion
}
