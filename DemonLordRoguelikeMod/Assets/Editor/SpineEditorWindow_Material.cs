using UnityEditor;
using UnityEngine;

public partial class SpineEditorWindow
{
    #region 材质球初始化页面

    private void DrawMaterialPage()
    {
        EditorGUILayout.BeginVertical(boxStyle);
        
        // ========== 说明区域 ==========
        EditorGUILayout.BeginVertical(new GUIStyle { 
            normal = new GUIStyleState { background = descBgTexture },
            padding = new RectOffset(10, 10, 10, 10)
        });
        
        EditorGUILayout.LabelField("功能说明", subHeaderStyle);
        EditorGUILayout.LabelField(
            "配置指定目录后，该目录下的 Spine 材质球会自动应用设置。\n" +
            "当导入新资源或资源发生变化时，也会自动更新对应的材质球属性。",
            EditorStyles.wordWrappedMiniLabel);
        
        EditorGUILayout.EndVertical();
        
        EditorGUILayout.Space(10);
        
        // ========== 目录配置列表 ==========
        DrawSectionHeader($"目录配置 ({materialConfig.directoryConfigs.Count})", "为不同目录设置不同的材质球属性");
        
        EditorGUILayout.BeginVertical(new GUIStyle { padding = new RectOffset(5, 5, 5, 5) });
        DrawMaterialConfigList();
        EditorGUILayout.EndVertical();
        
        EditorGUILayout.Space(10);
        
        // ========== 全局操作 ==========
        DrawSectionHeader("全局操作", "批量应用材质球设置");
        
        EditorGUILayout.BeginVertical(new GUIStyle { padding = new RectOffset(5, 5, 10, 10) });
        DrawMaterialGlobalActions();
        EditorGUILayout.EndVertical();
        
        EditorGUILayout.EndVertical();
    }

    private void DrawMaterialConfigList()
    {
        // 添加按钮
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        
        GUI.backgroundColor = new Color(0.2f, 0.6f, 0.9f);
        if (GUILayout.Button("+ 添加目录配置", GUILayout.Width(150), GUILayout.Height(28)))
        {
            materialConfig.directoryConfigs.Add(new SpineMaterialConfigAsset.DirectoryConfig());
            SpineMaterialConfig.Save(materialConfig);
        }
        GUI.backgroundColor = Color.white;
        
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(10);
        
        // 配置列表
        if (materialConfig.directoryConfigs.Count == 0)
        {
            EditorGUILayout.HelpBox("暂无配置，点击上方按钮添加目录配置", MessageType.Info);
        }
        else
        {
            materialScrollPosition = EditorGUILayout.BeginScrollView(materialScrollPosition, GUILayout.MaxHeight(300));
            
            for (int i = 0; i < materialConfig.directoryConfigs.Count; i++)
            {
                DrawMaterialConfigCard(i, materialConfig.directoryConfigs[i]);
                EditorGUILayout.Space(8);
            }
            
            EditorGUILayout.EndScrollView();
        }
    }

    private void DrawMaterialConfigCard(int index, SpineMaterialConfigAsset.DirectoryConfig config)
    {
        // 卡片背景
        EditorGUILayout.BeginVertical(new GUIStyle
        {
            normal = new GUIStyleState { background = cardBgTexture },
            border = new RectOffset(1, 1, 1, 1),
            padding = new RectOffset(12, 12, 10, 10)
        });
        
        // 头部：编号 + 删除按钮
        EditorGUILayout.BeginHorizontal();
        
        GUIStyle indexStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 13,
            normal = { textColor = new Color(0.2f, 0.4f, 0.7f) }
        };
        EditorGUILayout.LabelField($"配置 #{index + 1}", indexStyle);
        
        GUILayout.FlexibleSpace();
        
        GUI.backgroundColor = new Color(0.9f, 0.4f, 0.4f);
        if (GUILayout.Button("删除", GUILayout.Width(60)))
        {
            if (EditorUtility.DisplayDialog("确认删除", "确定要删除这个目录配置吗？", "删除", "取消"))
            {
                materialConfig.directoryConfigs.RemoveAt(index);
                SpineMaterialConfig.Save(materialConfig);
            }
        }
        GUI.backgroundColor = Color.white;
        
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(8);
        
        // 目录选择（支持拖拽）
        DrawDragDropPathField("目录:", ref config.folderPath, 40);
        
        EditorGUILayout.Space(8);
        
        // 属性设置
        EditorGUILayout.BeginVertical(new GUIStyle 
        { 
            normal = new GUIStyleState { background = propBgTexture },
            padding = new RectOffset(8, 8, 6, 6)
        });
        
        EditorGUILayout.LabelField("材质球属性", subHeaderStyle);
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Straight Alpha Texture:", GUILayout.Width(150));
        config.enableStraightAlpha = EditorGUILayout.Toggle(config.enableStraightAlpha, GUILayout.Width(20));
        
        GUIStyle hintStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            fontStyle = FontStyle.Italic
        };
        hintStyle.normal.textColor = new Color(0.5f, 0.5f, 0.5f);
        EditorGUILayout.LabelField(config.enableStraightAlpha ? "启用 (Straight)" : "禁用 (Premultiply)", hintStyle);
        
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.EndVertical();
        
        EditorGUILayout.Space(8);
        
        // 初始化按钮
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        
        GUI.backgroundColor = new Color(0.2f, 0.7f, 0.7f);
        if (GUILayout.Button("初始化此目录", GUILayout.Width(130), GUILayout.Height(28)))
        {
            SpineMaterialConfig.InitializeMaterialsInFolder(config.folderPath, config.enableStraightAlpha);
            SetStatus($"目录 {config.folderPath} 的材质球初始化完成！", MessageType.Info);
        }
        GUI.backgroundColor = Color.white;
        
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.EndVertical();
    }

    private void DrawMaterialGlobalActions()
    {
        EditorGUILayout.BeginHorizontal();
        
        // 保存配置
        if (GUILayout.Button("保存配置", GUILayout.Height(35)))
        {
            SpineMaterialConfig.Save(materialConfig);
            SetStatus("配置已保存！", MessageType.Info);
        }
        
        GUILayout.Space(10);
        
        // 全部初始化
        GUI.backgroundColor = new Color(0.2f, 0.7f, 0.7f);
        if (GUILayout.Button("全部初始化", GUILayout.Height(35)))
        {
            int configCount = materialConfig.directoryConfigs.Count;
            if (configCount == 0)
            {
                SetStatus("没有配置任何目录", MessageType.Warning);
            }
            else
            {
                SpineMaterialConfig.InitializeAllMaterials(materialConfig);
                SetStatus($"已初始化 {configCount} 个配置的目录", MessageType.Info);
            }
        }
        GUI.backgroundColor = Color.white;
        
        EditorGUILayout.EndHorizontal();
    }

    #endregion
}
