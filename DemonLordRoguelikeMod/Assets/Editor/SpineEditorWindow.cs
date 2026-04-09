using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace Spine.Editor
{
    public class SpineEditorWindow : EditorWindow
    {
        // 目录选择
        private string selectedFolderPath = "Assets";
        private Vector2 scrollPosition;
        
        // Addressables Group 选择
        private int selectedGroupIndex = 0;
        private List<string> groupNames = new List<string>();
        private List<AddressableAssetGroup> groups = new List<AddressableAssetGroup>();
        
        // 开关：是否添加 Addressables
        private bool enableAddressables = true;
        
        // 搜索结果
        private List<SpineAssetInfo> spineAssets = new List<SpineAssetInfo>();
        private bool showAssetList = true;
        
        // 执行状态
        private string statusMessage = "";
        private MessageType statusType = MessageType.Info;
        
        [MenuItem("工具/Spine/Addressable 管理器")]
        public static void ShowWindow()
        {
            var window = GetWindow<SpineEditorWindow>("Spine Addressable 管理器");
            window.minSize = new Vector2(500, 400);
            window.Show();
        }
        
        private void OnEnable()
        {
            RefreshAddressableGroups();
            RefreshAssetList();
        }
        
        /// <summary>
        /// 刷新 Addressable Groups 列表
        /// </summary>
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
            
            // 确保选中索引有效
            if (selectedGroupIndex >= groupNames.Count && groupNames.Count > 0)
            {
                selectedGroupIndex = 0;
            }
        }
        
        /// <summary>
        /// 刷新 Spine 资源列表
        /// </summary>
        private void RefreshAssetList()
        {
            spineAssets.Clear();
            
            if (!Directory.Exists(selectedFolderPath))
            {
                return;
            }
            
            // 查找所有 SkeletonDataAsset 文件 (Spine.Unity.SkeletonDataAsset)
            string[] guids = AssetDatabase.FindAssets("t:Spine.Unity.SkeletonDataAsset", new[] { selectedFolderPath });
            
            // 如果没找到，尝试使用文件名过滤作为备选
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
                    // 检查是否已经是 Addressable
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
            
            // 按路径排序
            spineAssets = spineAssets.OrderBy(a => a.path).ToList();
        }
        
        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            
            // 标题
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter
            };
            EditorGUILayout.LabelField("Spine Addressable 管理器", titleStyle);
            EditorGUILayout.Space(10);
            
            DrawSettingsSection();
            EditorGUILayout.Space(10);
            
            DrawAssetList();
            EditorGUILayout.Space(10);
            
            DrawActionButtons();
            EditorGUILayout.Space(10);
            
            DrawStatusMessage();
        }
        
        /// <summary>
        /// 绘制设置区域
        /// </summary>
        private void DrawSettingsSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("设置", EditorStyles.boldLabel);
            
            // 目录选择
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("目标目录:", GUILayout.Width(80));
            EditorGUILayout.TextField(selectedFolderPath);
            if (GUILayout.Button("浏览", GUILayout.Width(60)))
            {
                string selectedPath = EditorUtility.OpenFolderPanel("选择文件夹", selectedFolderPath, "");
                if (!string.IsNullOrEmpty(selectedPath))
                {
                    // 转换为相对路径
                    selectedFolderPath = GetRelativePath(selectedPath);
                    RefreshAssetList();
                    ClearStatus();
                }
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(5);
            
            // Addressables 开关
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("启用 Addressables:", GUILayout.Width(110));
            bool newEnableAddressables = EditorGUILayout.Toggle(enableAddressables, GUILayout.Width(20));
            if (newEnableAddressables != enableAddressables)
            {
                enableAddressables = newEnableAddressables;
                ClearStatus();
            }
            EditorGUILayout.EndHorizontal();
            
            // Group 选择（仅在开启 Addressables 时显示）
            if (enableAddressables)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("目标分组:", GUILayout.Width(80));
                
                if (groupNames.Count > 0)
                {
                    int newIndex = EditorGUILayout.Popup(selectedGroupIndex, groupNames.ToArray());
                    if (newIndex != selectedGroupIndex)
                    {
                        selectedGroupIndex = newIndex;
                        ClearStatus();
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("未找到 Addressable Group，请先创建分组。", MessageType.Warning);
                }
                EditorGUILayout.EndHorizontal();
                
                // 刷新按钮
                if (GUILayout.Button("刷新分组列表", GUILayout.Width(100)))
                {
                    RefreshAddressableGroups();
                }
            }
            
            EditorGUILayout.EndVertical();
        }
        
        /// <summary>
        /// 绘制资源列表
        /// </summary>
        private void DrawAssetList()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            // 折叠标题
            EditorGUILayout.BeginHorizontal();
            showAssetList = EditorGUILayout.Foldout(showAssetList, $"Spine 资源列表 ({spineAssets.Count})", true);
            if (GUILayout.Button("刷新列表", GUILayout.Width(80)))
            {
                RefreshAssetList();
                ClearStatus();
            }
            EditorGUILayout.EndHorizontal();
            
            if (showAssetList)
            {
                if (spineAssets.Count == 0)
                {
                    EditorGUILayout.HelpBox("所选目录下未找到 SkeletonData 资源。", MessageType.Info);
                }
                else
                {
                    scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.MaxHeight(250));
                    
                    // 表头
                    EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
                    EditorGUILayout.LabelField("名称", EditorStyles.toolbarButton, GUILayout.Width(150));
                    EditorGUILayout.LabelField("路径", EditorStyles.toolbarButton);
                    EditorGUILayout.LabelField("所属分组", EditorStyles.toolbarButton, GUILayout.Width(100));
                    EditorGUILayout.LabelField("状态", EditorStyles.toolbarButton, GUILayout.Width(60));
                    EditorGUILayout.EndHorizontal();
                    
                    // 列表内容
                    foreach (var asset in spineAssets)
                    {
                        EditorGUILayout.BeginHorizontal();
                        
                        // 名称（可点击选择）
                        if (GUILayout.Button(asset.name, EditorStyles.label, GUILayout.Width(150)))
                        {
                            EditorGUIUtility.PingObject(asset.asset);
                            Selection.activeObject = asset.asset;
                        }
                        
                        // 路径
                        EditorGUILayout.LabelField(asset.path, EditorStyles.miniLabel);
                        
                        // 当前 Group
                        EditorGUILayout.LabelField(asset.currentGroupName, EditorStyles.miniLabel, GUILayout.Width(100));
                        
                        // 状态
                        string status = asset.isAddressable ? "✓ 已启用" : "✗ 未启用";
                        GUIStyle statusStyle = new GUIStyle(EditorStyles.miniLabel);
                        statusStyle.normal.textColor = asset.isAddressable ? Color.green : Color.gray;
                        EditorGUILayout.LabelField(status, statusStyle, GUILayout.Width(60));
                        
                        EditorGUILayout.EndHorizontal();
                    }
                    
                    EditorGUILayout.EndScrollView();
                    
                    // 统计信息
                    int enabledCount = spineAssets.Count(a => a.isAddressable);
                    EditorGUILayout.LabelField($"统计: {enabledCount}/{spineAssets.Count} 个资源已启用 Addressable", EditorStyles.miniLabel);
                }
            }
            
            EditorGUILayout.EndVertical();
        }
        
        /// <summary>
        /// 绘制操作按钮
        /// </summary>
        private void DrawActionButtons()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("操作", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            
            GUI.backgroundColor = enableAddressables ? Color.green : Color.red;
            string buttonText = enableAddressables 
                ? $"添加到分组: {(groupNames.Count > 0 ? groupNames[selectedGroupIndex] : "无")}" 
                : "移除所有 Addressables";
            
            if (GUILayout.Button(buttonText, GUILayout.Height(40)))
            {
                if (enableAddressables)
                {
                    ApplyAddressablesToGroup();
                }
                else
                {
                    RemoveAllAddressables();
                }
            }
            GUI.backgroundColor = Color.white;
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(5);
            
            // 辅助按钮
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("选中所有资源"))
            {
                SelectAllAssets();
            }
            
            if (GUILayout.Button("打开 Addressables 分组窗口"))
            {
                EditorApplication.ExecuteMenuItem("Window/Asset Management/Addressables/Groups");
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
        }
        
        /// <summary>
        /// 绘制状态消息
        /// </summary>
        private void DrawStatusMessage()
        {
            if (!string.IsNullOrEmpty(statusMessage))
            {
                EditorGUILayout.HelpBox(statusMessage, statusType);
            }
        }
        
        /// <summary>
        /// 将 Spine 资源添加到指定 Group
        /// </summary>
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
            
            // 开始批量操作
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
            
            // 保存设置
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
            AssetDatabase.SaveAssets();
            
            RefreshAssetList();
            
            if (failCount == 0)
            {
                SetStatus($"成功将 {successCount} 个资源添加到分组 '{targetGroup.Name}'！", MessageType.Info);
            }
            else
            {
                SetStatus($"已将 {successCount} 个资源添加到分组 '{targetGroup.Name}'，失败: {failCount}", MessageType.Warning);
            }
        }
        
        /// <summary>
        /// 尝试将单个资源添加到 Group
        /// </summary>
        private bool TryAddToGroup(SpineAssetInfo asset, AddressableAssetGroup group, AddressableAssetSettings settings)
        {
            try
            {
                var entry = settings.CreateOrMoveEntry(asset.guid, group);
                if (entry != null)
                {
                    // 设置 Address 为资源名称
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
        
        /// <summary>
        /// 移除所有资源的 Addressables 设置
        /// </summary>
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
            
            // 开始批量操作
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
            
            // 保存设置
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
            AssetDatabase.SaveAssets();
            
            RefreshAssetList();
            SetStatus($"已移除 {successCount} 个资源的 Addressable 设置，跳过了 {skipCount} 个未启用的资源。", MessageType.Info);
        }
        
        /// <summary>
        /// 尝试移除单个资源的 Addressables 设置
        /// </summary>
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
        
        /// <summary>
        /// 在编辑器中选择所有资源
        /// </summary>
        private void SelectAllAssets()
        {
            if (spineAssets.Count > 0)
            {
                var objects = spineAssets.Select(a => a.asset).Where(o => o != null).ToArray();
                Selection.objects = objects;
                SetStatus($"已在编辑器中选中了 {objects.Length} 个资源。", MessageType.Info);
            }
        }
        
        /// <summary>
        /// 将绝对路径转换为相对路径
        /// </summary>
        private string GetRelativePath(string absolutePath)
        {
            string projectPath = Application.dataPath;
            if (absolutePath.StartsWith(projectPath))
            {
                return "Assets" + absolutePath.Substring(projectPath.Length);
            }
            return absolutePath;
        }
        
        /// <summary>
        /// 设置状态消息
        /// </summary>
        private void SetStatus(string message, MessageType type)
        {
            statusMessage = message;
            statusType = type;
        }
        
        /// <summary>
        /// 清除状态消息
        /// </summary>
        private void ClearStatus()
        {
            statusMessage = "";
        }
        
        /// <summary>
        /// Spine 资源信息
        /// </summary>
        private class SpineAssetInfo
        {
            public string guid;
            public string path;
            public string name;
            public Object asset;
            public bool isAddressable;
            public string currentGroupName;
        }
    }
}
