using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

public partial class SpineEditorWindow
{
    #region 数据刷新

    private void RefreshAddressableGroups()
    {
        groupNames.Clear();
        groups.Clear();

        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogWarning("未找到 Addressable Asset Settings，请先初始化 Addressables。");
            return;
        }

        foreach (var group in settings.groups)
        {
            if (group != null)
            {
                groups.Add(group);
                groupNames.Add(group.Name);
            }
        }

        if (selectedGroupIndex >= groupNames.Count && groupNames.Count > 0)
        {
            selectedGroupIndex = 0;
        }
    }

    private void RefreshAssetList()
    {
        spineAssets.Clear();

        if (!Directory.Exists(selectedFolderPath))
        {
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:Spine.Unity.SkeletonDataAsset", new[] { selectedFolderPath });

        if (guids.Length == 0)
        {
            string[] allAssetGuids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { selectedFolderPath });
            List<string> skeletonDataGuids = new List<string>();
            foreach (var guid in allAssetGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileName(path).EndsWith("_SkeletonData.asset"))
                {
                    skeletonDataGuids.Add(guid);
                }
            }
            guids = skeletonDataGuids.ToArray();
        }

        var settings = AddressableAssetSettingsDefaultObject.Settings;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Object asset = AssetDatabase.LoadAssetAtPath<Object>(path);

            if (asset != null)
            {
                bool isAddressable = false;
                string currentGroupName = "无";

                if (settings != null)
                {
                    var entry = settings.FindAssetEntry(guid);
                    if (entry != null && entry.parentGroup != null)
                    {
                        isAddressable = true;
                        currentGroupName = entry.parentGroup.Name;
                    }
                }

                spineAssets.Add(new SpineAssetInfo
                {
                    guid = guid,
                    path = path,
                    name = asset.name,
                    asset = asset,
                    isAddressable = isAddressable,
                    currentGroupName = currentGroupName
                });
            }
        }

        spineAssets = spineAssets.OrderBy(a => a.path).ToList();
    }

    #endregion

    #region 核心操作

    private void ApplyAddressablesToGroup()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            SetStatus("未找到 Addressable Asset Settings！", MessageType.Error);
            return;
        }

        if (groupNames.Count == 0 || selectedGroupIndex >= groups.Count)
        {
            SetStatus("未选择有效的 Addressable Group！", MessageType.Error);
            return;
        }

        var targetGroup = groups[selectedGroupIndex];
        if (targetGroup == null)
        {
            SetStatus("所选分组为空！", MessageType.Error);
            return;
        }

        int successCount = 0;
        int failCount = 0;

        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var asset in spineAssets)
            {
                if (TryAddToGroup(asset, targetGroup, settings))
                {
                    successCount++;
                }
                else
                {
                    failCount++;
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
        AssetDatabase.SaveAssets();

        RefreshAssetList();

        if (failCount == 0)
        {
            SetStatus($"成功将 {successCount} 个资源添加到分组 '{targetGroup.Name}'！", MessageType.Info);
        }
        else
        {
            SetStatus($"已将 {successCount} 个资源添加到分组，失败: {failCount}", MessageType.Warning);
        }
    }

    private bool TryAddToGroup(SpineAssetInfo asset, AddressableAssetGroup group, AddressableAssetSettings settings)
    {
        try
        {
            var entry = settings.CreateOrMoveEntry(asset.guid, group);
            if (entry != null)
            {
                entry.address = asset.name;
                return true;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"添加失败 {asset.path}: {e.Message}");
        }
        return false;
    }

    private void RemoveAllAddressables()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            SetStatus("未找到 Addressable Asset Settings！", MessageType.Error);
            return;
        }

        int successCount = 0;
        int skipCount = 0;

        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var asset in spineAssets)
            {
                if (asset.isAddressable)
                {
                    if (TryRemoveFromAddressables(asset, settings))
                    {
                        successCount++;
                    }
                }
                else
                {
                    skipCount++;
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
        AssetDatabase.SaveAssets();

        RefreshAssetList();
        SetStatus($"已移除 {successCount} 个资源的 Addressable 设置，跳过 {skipCount} 个", MessageType.Info);
    }

    private bool TryRemoveFromAddressables(SpineAssetInfo asset, AddressableAssetSettings settings)
    {
        try
        {
            settings.RemoveAssetEntry(asset.guid);
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"移除失败 {asset.path}: {e.Message}");
            return false;
        }
    }

    #endregion
}
