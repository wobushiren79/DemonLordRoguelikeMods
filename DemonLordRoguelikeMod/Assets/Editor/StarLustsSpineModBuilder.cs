using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using Spine.Unity;

/// <summary>
/// StarLustsSpine Mod 一键构建器。
/// <para>流程：扫描 Assets/ModResource/Spine/StarLusts 下全部 SkeletonData 资源 → 同步进 Mod_StarLustsSpine 分组
/// （Address=资产名，与主游戏幻化药 ItemsInfo.other_data 的 ui_show_res 键引用的资源名一致）→ 临时排除其他分组 →
/// 切换 Profile 输出路径到 Mods/StarLustsSpine → 构建 → 恢复其他分组。</para>
/// <para>Mod 名 = 输出目录名（主游戏 ModManager 按 Mods/&lt;目录名&gt;/catalog.bin 发现 Mod）；分组名只影响 bundle 文件名。</para>
/// <para>构建不清理 JsonText 子目录（Mod 的道具/语言配置由生成脚本写入该目录，属 Mod 包一部分）。</para>
/// <para>构建末尾自动调主项目生成脚本导出 Excel→JsonText 并部署到主项目（导出逻辑单一真实源=python 脚本，此处仅调用；
/// 主项目根目录经菜单「设置主项目根目录(自动导出部署用)」配置，未配置时跳过并告警、不影响构建产物）。</para>
/// <para>与其他 ui_show 系 Mod 一致：StarLusts 资源全部为 ui_show 系（只改详情UI高清展示），SkeletonData scale 统一保持 0.01，
/// 详情UI尺寸由道具 other_data 的 ui_show_data 键控制；出药规则为纯数字皮肤(1/2/3)出皮肤药、无数字皮肤出 default 皮药。</para>
/// </summary>
public static class StarLustsSpineModBuilder
{
    /// <summary>Mod 名（= Mods 下的目录名，主游戏按此名加载）</summary>
    public const string ModName = "StarLustsSpine";
    /// <summary>Addressables 分组名</summary>
    public const string GroupName = "Mod_StarLustsSpine";
    /// <summary>Spine 资源源目录</summary>
    public const string SourceFolder = "Assets/ModResource/Spine/StarLusts";
    /// <summary>ui_show 系 SkeletonData 缩放（保持导入默认 0.01；详情UI尺寸由道具 other_data 的 ui_show_data 键控制）</summary>
    public const float UIShowSkeletonDataScale = 0.01f;

    /// <summary>构建输出目录（项目根相对路径）</summary>
    private static string OutputDir => $"Mods/{ModName}";

    #region 菜单入口

    /// <summary>
    /// 一键构建：同步分组条目 + 构建到 Mods/StarLustsSpine
    /// </summary>
    [MenuItem("工具/Mod/StarLustsSpine/一键构建(同步分组+构建)", false, 100)]
    public static void BuildMod()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("[StarLustsSpine] 未找到 Addressable Asset Settings");
            return;
        }

        // 1.同步分组条目
        var group = GetOrCreateGroup(settings);
        EnsureGroupSchema(settings, group);
        int entryCount = SyncGroupEntries(settings, group);

        // 2.SkeletonData 缩放校准（全部 ui_show 系,统一复位 0.01,幂等）
        int scaleCount = ApplyUIShowSkeletonDataScale();

        // 3.隔离构建（临时排除其他分组，构建后恢复）
        var savedIncludeInBuild = DisableOtherGroups(settings, group);
        try
        {
            // 3.切换输出 Profile 到 Mods/StarLustsSpine
            SetupProfile(settings);
            // 4.清理旧产物（保留 JsonText 配置目录）
            CleanOutputDir();
            // 5.构建
            AssetDatabase.SaveAssets();
            AddressableAssetSettings.BuildPlayerContent();
            // 6.catalog/settings 不跟随分组 BuildPath（默认落在 Library/com.unity.addressables/aa/<平台>），拷进 Mod 目录
            int catalogCount = CopyCatalogFiles();
            // 7.自动导出 Excel→JsonText 并部署到主项目（防「只构建不导出」导致 JsonText 缺失、主游戏道具合并不生效）
            string exportResult = ExportJsonTextAndDeploy();
            Debug.Log($"[StarLustsSpine] 构建完成：{entryCount} 个资源(拆分bundle,缩放调整 {scaleCount} 个)、catalog 文件 {catalogCount}/3、{exportResult} → {Path.GetFullPath(OutputDir)}\n" +
                      $"下一步（未自动部署时）：把 {OutputDir} 整个目录（含 JsonText）拷贝到主游戏项目的 Mods/{ModName}");
            EditorUtility.RevealInFinder(Path.GetFullPath(OutputDir));
        }
        finally
        {
            // 6.恢复其他分组的 IncludeInBuild
            RestoreOtherGroups(savedIncludeInBuild);
            AssetDatabase.SaveAssets();
        }
    }

    /// <summary>
    /// 仅同步分组条目（不构建），用于先检查条目列表
    /// </summary>
    [MenuItem("工具/Mod/StarLustsSpine/仅同步分组条目", false, 101)]
    public static void SyncGroupEntriesOnly()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("[StarLustsSpine] 未找到 Addressable Asset Settings");
            return;
        }
        var group = GetOrCreateGroup(settings);
        EnsureGroupSchema(settings, group);
        int entryCount = SyncGroupEntries(settings, group);
        AssetDatabase.SaveAssets();
        Debug.Log($"[StarLustsSpine] 分组条目同步完成：{entryCount} 个 SkeletonData 资源（Address=资产名）");
    }

    /// <summary>
    /// 设置主项目根目录（一键构建后自动导出/部署用，EditorPrefs 持久化，每台机器只需设置一次）
    /// </summary>
    [MenuItem("工具/Mod/StarLustsSpine/设置主项目根目录(自动导出部署用)", false, 200)]
    public static void SetMainProjectRoot()
    {
        string current = EditorPrefs.GetString(EditorPrefsKeyMainProjectRoot, "");
        string selected = EditorUtility.OpenFolderPanel("选择主项目根目录（其 .claude/scripts 下含生成脚本）", current, "");
        if (!string.IsNullOrEmpty(selected))
        {
            EditorPrefs.SetString(EditorPrefsKeyMainProjectRoot, selected.Replace("\\", "/"));
            Debug.Log($"[StarLustsSpine] 主项目根目录已设置: {selected}");
        }
    }

    #endregion

    #region 自动导出部署

    /// <summary>主项目根目录的 EditorPrefs 键（用于定位主项目的 run-python.ps1/gen_starlusts_spine_mod.py 生成脚本）</summary>
    private const string EditorPrefsKeyMainProjectRoot = "StarLustsSpineModBuilder.MainProjectRoot";
    /// <summary>自动导出+部署的进程超时（毫秒）</summary>
    private const int ExportTimeoutMs = 180000;

    /// <summary>
    /// 构建后自动导出 Excel→JsonText 并部署到主项目：调主项目 .claude/scripts 的 run-python.ps1 包装跑 gen_starlusts_spine_mod.py(export --deploy-main)，
    /// 导出逻辑单一真实源在 python 脚本（C# 不重复实现）。主项目根目录经 EditorPrefs 配置（菜单「设置主项目根目录」）；未配置/脚本缺失/执行失败均只告警，不影响构建产物。
    /// </summary>
    /// <returns>执行结果描述（拼进构建完成日志）</returns>
    private static string ExportJsonTextAndDeploy()
    {
        string mainRoot = EditorPrefs.GetString(EditorPrefsKeyMainProjectRoot, "");
        if (string.IsNullOrEmpty(mainRoot) || !Directory.Exists(mainRoot))
        {
            Debug.LogWarning("[StarLustsSpine] 未配置主项目根目录，已跳过自动导出/部署（菜单：工具/Mod/StarLustsSpine/设置主项目根目录(自动导出部署用)）");
            return "导出部署=跳过(未配置主项目路径)";
        }
        string runPythonPs1 = Path.Combine(mainRoot, ".claude/scripts/run-python.ps1");
        string exportPy = Path.Combine(mainRoot, ".claude/scripts/gen_starlusts_spine_mod.py");
        if (!File.Exists(runPythonPs1) || !File.Exists(exportPy))
        {
            Debug.LogWarning($"[StarLustsSpine] 主项目缺少导出脚本，已跳过自动导出/部署: {exportPy}");
            return "导出部署=跳过(脚本缺失)";
        }
        string modRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{runPythonPs1}\" \"{exportPy}\" export --mod-project \"{modRoot}\" --deploy-main \"{mainRoot}\"";
        var startInfo = new System.Diagnostics.ProcessStartInfo("powershell.exe", arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using (var process = System.Diagnostics.Process.Start(startInfo))
        {
            //先读尽两个流再等退出（防管道缓冲填满死锁）
            string stdOut = process.StandardOutput.ReadToEnd();
            string stdErr = process.StandardError.ReadToEnd();
            if (!process.WaitForExit(ExportTimeoutMs))
            {
                try { process.Kill(); } catch { }
                Debug.LogError("[StarLustsSpine] 自动导出超时已终止");
                return "导出部署=超时";
            }
            if (process.ExitCode != 0)
            {
                Debug.LogError($"[StarLustsSpine] 自动导出失败(exit={process.ExitCode})：\n{stdOut}\n{stdErr}");
                return $"导出部署=失败(exit={process.ExitCode})";
            }
            Debug.Log($"[StarLustsSpine] 自动导出+部署完成：\n{stdOut.Trim()}");
            return "导出部署=完成";
        }
    }

    #endregion

    #region 分组同步

    /// <summary>
    /// 获取或创建 Mod 分组
    /// </summary>
    private static AddressableAssetGroup GetOrCreateGroup(AddressableAssetSettings settings)
    {
        var group = settings.FindGroup(GroupName);
        if (group != null)
            return group;
        return settings.CreateGroup(GroupName, false, false, false, null,
            typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
    }

    /// <summary>
    /// 确保分组 schema 设置：PackSeparately（每个 SkeletonData 一个 bundle，幻化药使用时按需加载单个资源，
    /// 避免 PackTogether 单大 bundle 首次全量加载慢）
    /// </summary>
    private static void EnsureGroupSchema(AddressableAssetSettings settings, AddressableAssetGroup group)
    {
        var schema = group.GetSchema<BundledAssetGroupSchema>();
        if (schema != null && schema.BundleMode != BundledAssetGroupSchema.BundlePackingMode.PackSeparately)
        {
            schema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackSeparately;
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.GroupSchemaModified, group, true, true);
            Debug.Log("[StarLustsSpine] 分组打包模式已设为 PackSeparately（每资源一个 bundle）");
        }
    }

    /// <summary>
    /// 把 StarLusts 下全部 SkeletonDataAsset.scale 统一复位为 UIShowSkeletonDataScale（0.01）：
    /// StarLusts 资源全部是 ui_show 系（只改详情UI高清展示），详情UI尺寸由道具 other_data 的 ui_show_data 键控制。幂等可重跑。
    /// </summary>
    /// <returns>本次实际修改的资产数</returns>
    private static int ApplyUIShowSkeletonDataScale()
    {
        var guids = AssetDatabase.FindAssets("t:SkeletonDataAsset", new[] { SourceFolder });
        int count = 0;
        foreach (var guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            var skeletonDataAsset = AssetDatabase.LoadAssetAtPath<SkeletonDataAsset>(assetPath);
            if (skeletonDataAsset == null)
                continue;
            if (!Mathf.Approximately(skeletonDataAsset.scale, UIShowSkeletonDataScale))
            {
                skeletonDataAsset.scale = UIShowSkeletonDataScale;
                EditorUtility.SetDirty(skeletonDataAsset);
                count++;
            }
        }
        if (count > 0)
            AssetDatabase.SaveAssets();
        return count;
    }

    /// <summary>
    /// 同步分组条目：清空后重新添加源目录下全部 SkeletonData 资源，Address=资产名（不含扩展名）。
    /// 依赖资源（Atlas/Material/Texture）由构建自动打进 bundle，无需单独条目。
    /// </summary>
    private static int SyncGroupEntries(AddressableAssetSettings settings, AddressableAssetGroup group)
    {
        // 清空旧条目（幂等重扫）
        var oldEntries = group.entries.ToList();
        foreach (var entry in oldEntries)
            group.RemoveAssetEntry(entry);

        var guids = AssetDatabase.FindAssets("t:SkeletonDataAsset", new[] { SourceFolder });
        int count = 0;
        foreach (var guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            string address = Path.GetFileNameWithoutExtension(assetPath);
            var entry = settings.CreateOrMoveEntry(guid, group, false, false);
            if (entry == null)
            {
                Debug.LogWarning($"[StarLustsSpine] 条目创建失败: {assetPath}");
                continue;
            }
            if (entry.address != address)
                entry.SetAddress(address);
            count++;
        }
        settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, group, true, true);
        return count;
    }

    #endregion

    #region 构建隔离与Profile

    /// <summary>
    /// 临时把其他分组的 IncludeInBuild 置 false（只构建本 Mod），返回原值快照供恢复
    /// </summary>
    private static Dictionary<AddressableAssetGroup, bool> DisableOtherGroups(AddressableAssetSettings settings, AddressableAssetGroup targetGroup)
    {
        var snapshot = new Dictionary<AddressableAssetGroup, bool>();
        foreach (var group in settings.groups)
        {
            if (group == null || group == targetGroup)
                continue;
            var schema = group.GetSchema<BundledAssetGroupSchema>();
            if (schema == null)
                continue;
            snapshot[group] = schema.IncludeInBuild;
            if (schema.IncludeInBuild)
            {
                schema.IncludeInBuild = false;
                settings.SetDirty(AddressableAssetSettings.ModificationEvent.GroupSchemaModified, group, true, true);
            }
        }
        return snapshot;
    }

    /// <summary>
    /// 恢复其他分组的 IncludeInBuild 原值
    /// </summary>
    private static void RestoreOtherGroups(Dictionary<AddressableAssetGroup, bool> snapshot)
    {
        foreach (var kvp in snapshot)
        {
            var schema = kvp.Key.GetSchema<BundledAssetGroupSchema>();
            if (schema != null && schema.IncludeInBuild != kvp.Value)
                schema.IncludeInBuild = kvp.Value;
        }
    }

    /// <summary>
    /// 切换 Profile：新增/复用与 Mod 同名的 Profile，把构建/加载路径都指向 Mods/StarLustsSpine 并设为激活
    /// </summary>
    private static void SetupProfile(AddressableAssetSettings settings)
    {
        var profileSettings = settings.profileSettings;
        string profileId = profileSettings.GetProfileId(ModName);
        if (string.IsNullOrEmpty(profileId))
            profileId = profileSettings.AddProfile(ModName, null);
        // SetValue 第二参为变量名（非变量ID）
        foreach (string variableName in new[] { "Local.BuildPath", "Local.LoadPath", "Remote.BuildPath", "Remote.LoadPath" })
            profileSettings.SetValue(profileId, variableName, OutputDir);
        settings.activeProfileId = profileId;
        settings.SetDirty(AddressableAssetSettings.ModificationEvent.ProfileModified, null, true, true);
    }

    /// <summary>
    /// 清理旧构建产物（保留 JsonText 配置目录——Mod 道具/语言配置由生成脚本维护在那里）
    /// </summary>
    private static void CleanOutputDir()
    {
        if (!Directory.Exists(OutputDir))
            return;
        foreach (var file in Directory.GetFiles(OutputDir))
            File.Delete(file);
        foreach (var dir in Directory.GetDirectories(OutputDir))
        {
            if (Path.GetFileName(dir) == "JsonText")
                continue;
            Directory.Delete(dir, true);
        }
    }

    /// <summary>
    /// 把 catalog.bin/catalog.hash/settings.json 从引擎默认输出目录拷进 Mod 目录。
    /// catalog 输出路径是 settings 级（BuildRemoteCatalog=false 时固定 Addressables.BuildPath），不跟随分组的 Local.BuildPath；
    /// 主游戏 ModManager 只读 Mods/&lt;Mod名&gt;/catalog.bin，三个文件拷齐与旧 Mod 目录结构保持一致。
    /// </summary>
    /// <returns>成功拷贝的文件数（0~3）</returns>
    private static int CopyCatalogFiles()
    {
        string aaBuildPath = UnityEngine.AddressableAssets.Addressables.BuildPath;
        int count = 0;
        foreach (string fileName in new[] { "catalog.bin", "catalog.hash", "settings.json" })
        {
            string src = Path.Combine(aaBuildPath, fileName);
            if (File.Exists(src))
            {
                File.Copy(src, Path.Combine(OutputDir, fileName), true);
                count++;
            }
            else
            {
                Debug.LogWarning($"[StarLustsSpine] 未找到 catalog 文件: {src}");
            }
        }
        return count;
    }

    #endregion
}
