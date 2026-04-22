using System.Linq;
using UnityEditor;
using UnityEngine;

public partial class SpineEditorWindow
{
    #region Addressable 管理页面

    private void DrawAddressablePage()
    {
        EditorGUILayout.BeginVertical(boxStyle);
        
        // ========== 1. 资源扫描区域 ==========
        DrawSectionHeader("1. 资源扫描", "选择目录并扫描 Spine SkeletonData 资源");
        
        EditorGUILayout.BeginVertical(new GUIStyle { padding = new RectOffset(5, 5, 5, 5) });
        
        // 目录选择（支持拖拽）
        DrawDragDropPathField("目标目录:", ref selectedFolderPath, 70, () => {
            isLoaded = false;
            spineAssets.Clear();
            ClearStatus();
        });
        
        EditorGUILayout.Space(8);
        
        // 读取按钮
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        
        GUI.backgroundColor = new Color(0.2f, 0.6f, 0.9f);
        GUIContent readBtnContent = new GUIContent(" 扫描资源 ", "点击扫描指定目录下的 Spine 资源");
        if (GUILayout.Button(readBtnContent, GUILayout.Width(120), GUILayout.Height(28)))
        {
            RefreshAssetList();
            isLoaded = true;
            SetStatus($"扫描完成，共找到 {spineAssets.Count} 个资源", MessageType.Info);
        }
        GUI.backgroundColor = Color.white;
        
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.EndVertical();
        
        EditorGUILayout.Space(5);
        
        // ========== 2. 资源列表区域 ==========
        DrawSectionHeader($"2. 资源列表 ({spineAssets.Count})", "扫描结果与 Addressable 状态");
        
        EditorGUILayout.BeginVertical(new GUIStyle { padding = new RectOffset(5, 5, 5, 5) });
        DrawAssetListSection();
        EditorGUILayout.EndVertical();
        
        EditorGUILayout.Space(5);
        
        // ========== 3. Addressable 设置区域 ==========
        DrawSectionHeader("3. Addressable 设置", "配置要应用的分组");
        
        EditorGUILayout.BeginVertical(new GUIStyle { padding = new RectOffset(5, 5, 5, 5) });
        DrawAddressableSettings();
        EditorGUILayout.EndVertical();
        
        EditorGUILayout.Space(5);
        
        // ========== 4. 操作区域 ==========
        DrawSectionHeader("4. 批量操作", "应用或移除 Addressable 设置");
        
        EditorGUILayout.BeginVertical(new GUIStyle { padding = new RectOffset(5, 5, 10, 10) });
        DrawActionButtons();
        EditorGUILayout.EndVertical();
        
        EditorGUILayout.EndVertical();
    }

    private void DrawAssetListSection()
    {
        EditorGUILayout.BeginHorizontal();
        
        GUILayout.FlexibleSpace();
        
        GUI.enabled = isLoaded && spineAssets.Count > 0;
        
        if (GUILayout.Button("清空列表", GUILayout.Width(80)))
        {
            spineAssets.Clear();
            isLoaded = false;
            ClearStatus();
        }
        
        GUI.enabled = isLoaded;
        
        if (GUILayout.Button("刷新列表", GUILayout.Width(80)))
        {
            RefreshAssetList();
            SetStatus($"刷新完成，共找到 {spineAssets.Count} 个资源", MessageType.Info);
        }
        
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(5);
        
        // 列表内容
        if (!isLoaded)
        {
            EditorGUILayout.HelpBox("请先点击【扫描资源】按钮加载 Spine 资源", MessageType.Info);
        }
        else if (spineAssets.Count == 0)
        {
            EditorGUILayout.HelpBox("所选目录下未找到 SkeletonData 资源", MessageType.Warning);
        }
        else
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.MinHeight(150), GUILayout.MaxHeight(250));
            
            // 表头
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField("名称", EditorStyles.toolbarButton, GUILayout.Width(140));
            EditorGUILayout.LabelField("路径", EditorStyles.toolbarButton);
            EditorGUILayout.LabelField("分组", EditorStyles.toolbarButton, GUILayout.Width(80));
            EditorGUILayout.LabelField("状态", EditorStyles.toolbarButton, GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();
            
            // 列表内容
            for (int i = 0; i < spineAssets.Count; i++)
            {
                var asset = spineAssets[i];
                bool isEven = i % 2 == 0;
                
                EditorGUILayout.BeginHorizontal(isEven ? GUIStyle.none : oddRowStyle);
                
                // 名称
                if (GUILayout.Button(asset.name, EditorStyles.label, GUILayout.Width(140)))
                {
                    var obj = asset.GetAsset();
                    if (obj != null)
                    {
                        EditorGUIUtility.PingObject(obj);
                        Selection.activeObject = obj;
                    }
                }
                
                // 路径
                EditorGUILayout.LabelField(asset.path, EditorStyles.miniLabel);
                
                // 分组
                EditorGUILayout.LabelField(asset.currentGroupName, EditorStyles.miniLabel, GUILayout.Width(80));
                
                // 状态
                string status = asset.isAddressable ? "✓ 已启用" : "✗ 未启用";
                GUIStyle statusStyle = new GUIStyle(EditorStyles.miniLabel);
                statusStyle.normal.textColor = asset.isAddressable ? new Color(0.2f, 0.7f, 0.2f) : new Color(0.6f, 0.6f, 0.6f);
                EditorGUILayout.LabelField(status, statusStyle, GUILayout.Width(60));
                
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.EndScrollView();
            
            // 统计
            int enabledCount = spineAssets.Count(a => a.isAddressable);
            EditorGUILayout.Space(5);
            GUIStyle statsStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleRight,
                fontStyle = FontStyle.Bold
            };
            EditorGUILayout.LabelField($"已启用: {enabledCount} / 总数: {spineAssets.Count}", statsStyle);
        }
    }

    private void DrawAddressableSettings()
    {
        // Addressables 开关
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("启用 Addressables:", GUILayout.Width(120));
        bool newEnableAddressables = EditorGUILayout.Toggle(enableAddressables, GUILayout.Width(20));
        if (newEnableAddressables != enableAddressables)
        {
            enableAddressables = newEnableAddressables;
            ClearStatus();
        }
        
        GUILayout.FlexibleSpace();
        
        // 状态指示
        GUIStyle indicatorStyle = new GUIStyle(EditorStyles.miniLabel);
        indicatorStyle.normal.textColor = enableAddressables ? new Color(0.2f, 0.7f, 0.2f) : new Color(0.8f, 0.3f, 0.3f);
        EditorGUILayout.LabelField(enableAddressables ? "● 添加模式" : "● 移除模式", indicatorStyle);
        
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(5);
        
        // Group 选择
        if (enableAddressables)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("目标分组:", GUILayout.Width(70));
            
            if (groupNames.Count > 0)
            {
                selectedGroupIndex = EditorGUILayout.Popup(selectedGroupIndex, groupNames.ToArray(), GUILayout.Width(200));
                
                GUILayout.Space(10);
                
                if (GUILayout.Button("刷新", GUILayout.Width(60)))
                {
                    RefreshAddressableGroups();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("未找到 Addressable Group，请先创建分组", MessageType.Warning);
            }
            EditorGUILayout.EndHorizontal();
        }
    }

    private void DrawActionButtons()
    {
        EditorGUILayout.BeginHorizontal();
        
        // 主要操作按钮
        GUI.enabled = isLoaded && spineAssets.Count > 0;
        
        if (enableAddressables)
        {
            GUI.backgroundColor = new Color(0.2f, 0.7f, 0.3f);
            string btnText = groupNames.Count > 0 
                ? $"添加到: {groupNames[selectedGroupIndex]}"
                : "添加到 Addressables";
            
            if (GUILayout.Button(btnText, GUILayout.Height(40)))
            {
                ApplyAddressablesToGroup();
            }
        }
        else
        {
            GUI.backgroundColor = new Color(0.8f, 0.3f, 0.3f);
            
            if (GUILayout.Button("移除所有 Addressables", GUILayout.Height(40)))
            {
                if (EditorUtility.DisplayDialog("确认移除", 
                    $"确定要移除这 {spineAssets.Count} 个资源的 Addressables 设置吗？", 
                    "移除", "取消"))
                {
                    RemoveAllAddressables();
                }
            }
        }
        
        GUI.backgroundColor = Color.white;
        GUI.enabled = true;
        
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(10);
        
        // 辅助按钮
        EditorGUILayout.BeginHorizontal();
        
        GUILayout.FlexibleSpace();
        
        GUI.enabled = isLoaded && spineAssets.Count > 0;
        if (GUILayout.Button("选中所有资源", GUILayout.Width(120)))
        {
            SelectAllAssets();
        }
        GUI.enabled = true;
        
        GUILayout.Space(10);
        
        if (GUILayout.Button("打开 Addressables 窗口", GUILayout.Width(160)))
        {
            EditorApplication.ExecuteMenuItem("Window/Asset Management/Addressables/Groups");
        }
        
        GUILayout.FlexibleSpace();
        
        EditorGUILayout.EndHorizontal();
    }

    #endregion
}
