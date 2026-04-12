using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

public partial class SpineEditorWindow : EditorWindow
{
    // ==================== Tab 分页 ====================
    private enum TabPage { Addressable, Material }
    private TabPage currentTab = TabPage.Addressable;
    private string[] tabNames = new[] { "Addressable 管理", "材质球初始化" };

    // ==================== Addressable 管理 ====================
    private string selectedFolderPath = "Assets";
    private Vector2 scrollPosition;
    private int selectedGroupIndex = 0;
    private List<string> groupNames = new List<string>();
    private List<AddressableAssetGroup> groups = new List<AddressableAssetGroup>();
    private bool enableAddressables = true;
    private List<SpineAssetInfo> spineAssets = new List<SpineAssetInfo>();
    private bool isLoaded = false;

    // ==================== 材质球初始化 ====================
    private SpineMaterialConfigAsset materialConfig;
    private Vector2 materialScrollPosition;

    // ==================== 状态消息 ====================
    private string statusMessage = "";
    private MessageType statusType = MessageType.Info;
    private float statusMessageTime = 0f;

    // ==================== 样式缓存 ====================
    private GUIStyle titleStyle;
    private GUIStyle sectionHeaderStyle;
    private GUIStyle boxStyle;
    private GUIStyle tabStyle;
    private GUIStyle tabActiveStyle;
    private GUIStyle subHeaderStyle;
    private bool stylesInitialized = false;

    #region 窗口生命周期

    [MenuItem("工具/Spine/Spine 资源管理器", false, 1)]
    public static void ShowWindow()
    {
        var window = GetWindow<SpineEditorWindow>("Spine 资源管理器");
        window.minSize = new Vector2(550, 500);
        window.Show();
    }

    private void OnEnable()
    {
        RefreshAddressableGroups();
        isLoaded = false;
        spineAssets.Clear();
        materialConfig = SpineMaterialConfig.Load();
    }

    #endregion

    #region GUI 渲染

    private void OnGUI()
    {
        InitStyles();
        
        // 绘制标题栏
        DrawHeader();
        
        EditorGUILayout.Space(5);
        
        // 绘制Tab导航
        DrawTabs();
        
        EditorGUILayout.Space(5);
        
        // 根据当前Tab绘制内容
        switch (currentTab)
        {
            case TabPage.Addressable:
                DrawAddressablePage();
                break;
            case TabPage.Material:
                DrawMaterialPage();
                break;
        }
        
        // 绘制状态栏（固定在底部）
        DrawStatusBar();
    }

    private void InitStyles()
    {
        if (stylesInitialized) return;

        // 标题样式
        titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 18,
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold
        };

        // 区块标题样式
        sectionHeaderStyle = new GUIStyle(EditorStyles.helpBox)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft,
            padding = new RectOffset(10, 10, 5, 5)
        };
        sectionHeaderStyle.normal.textColor = new Color(0.2f, 0.4f, 0.7f);

        // 子标题样式
        subHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 11,
            fontStyle = FontStyle.Bold
        };

        // Tab样式
        tabStyle = new GUIStyle(EditorStyles.toolbarButton)
        {
            fontSize = 13,
            fontStyle = FontStyle.Normal,
            alignment = TextAnchor.MiddleCenter,
            padding = new RectOffset(15, 15, 12, 12),
            normal = { textColor = EditorStyles.label.normal.textColor }
        };

        // Tab激活样式
        tabActiveStyle = new GUIStyle(tabStyle)
        {
            fontStyle = FontStyle.Bold,
            fontSize = 14
        };
        tabActiveStyle.normal.background = MakeTexture(2, 2, new Color(0.2f, 0.5f, 0.9f, 0.8f));
        tabActiveStyle.normal.textColor = Color.white;

        // 盒子样式
        boxStyle = new GUIStyle(EditorStyles.helpBox)
        {
            padding = new RectOffset(10, 10, 10, 10)
        };

        stylesInitialized = true;
    }

    private void DrawHeader()
    {
        EditorGUILayout.BeginVertical(new GUIStyle { padding = new RectOffset(10, 10, 10, 5) });
        
        EditorGUILayout.LabelField("Spine 资源管理器", titleStyle);
        
        GUIStyle subtitleStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
        {
            fontSize = 10
        };
        EditorGUILayout.LabelField("管理 Spine Addressable 资源与材质球设置", subtitleStyle);
        
        EditorGUILayout.EndVertical();
    }

    private void DrawTabs()
    {
        // 使用BeginVertical+BeginHorizontal来确保高度生效
        EditorGUILayout.BeginVertical(GUILayout.Height(70));
        EditorGUILayout.BeginHorizontal(GUIStyle.none, GUILayout.Height(70));
        
        GUILayout.FlexibleSpace();
        
        for (int i = 0; i < tabNames.Length; i++)
        {
            bool isActive = (TabPage)i == currentTab;
            GUIStyle style = isActive ? tabActiveStyle : tabStyle;
            
            // Tab图标
            string icon = i == 0 ? "▼" : "◆";
            string label = $" {icon} {tabNames[i]} ";
            
            if (GUILayout.Button(label, style, GUILayout.Width(180), GUILayout.Height(55)))
            {
                currentTab = (TabPage)i;
                ClearStatus();
            }
            
            if (i < tabNames.Length - 1)
            {
                GUILayout.Space(10);
            }
        }
        
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    #endregion

    #region 状态栏

    private void DrawStatusBar()
    {
        if (string.IsNullOrEmpty(statusMessage))
            return;
        
        EditorGUILayout.Space(5);
        
        // 根据消息类型设置颜色
        Color bgColor;
        switch (statusType)
        {
            case MessageType.Error:
                bgColor = new Color(0.8f, 0.3f, 0.3f, 0.2f);
                break;
            case MessageType.Warning:
                bgColor = new Color(0.9f, 0.7f, 0.2f, 0.2f);
                break;
            default:
                bgColor = new Color(0.2f, 0.6f, 0.9f, 0.2f);
                break;
        }
        
        EditorGUILayout.BeginVertical(new GUIStyle 
        { 
            normal = new GUIStyleState { background = MakeTexture(2, 2, bgColor) },
            padding = new RectOffset(10, 10, 8, 8)
        });
        
        EditorGUILayout.LabelField(statusMessage, EditorStyles.miniLabel);
        
        EditorGUILayout.EndVertical();
    }

    #endregion

    #region 辅助方法

    private void DrawSectionHeader(string title, string description)
    {
        EditorGUILayout.BeginHorizontal(sectionHeaderStyle);
        
        EditorGUILayout.LabelField(title, subHeaderStyle, GUILayout.ExpandWidth(false));
        
        GUIStyle descStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            fontSize = 10,
            fontStyle = FontStyle.Italic
        };
        descStyle.normal.textColor = new Color(0.5f, 0.5f, 0.5f);
        
        GUILayout.Space(10);
        EditorGUILayout.LabelField(description, descStyle);
        
        EditorGUILayout.EndHorizontal();
    }

    private void SelectAllAssets()
    {
        if (spineAssets.Count > 0)
        {
            var objects = spineAssets.Select(a => a.asset).Where(o => o != null).ToArray();
            Selection.objects = objects;
            SetStatus($"已选中 {objects.Length} 个资源", MessageType.Info);
        }
    }

    private string GetRelativePath(string absolutePath)
    {
        string projectPath = Application.dataPath;
        if (absolutePath.StartsWith(projectPath))
        {
            return "Assets" + absolutePath.Substring(projectPath.Length);
        }
        return absolutePath;
    }

    private bool DrawDragDropPathField(string label, ref string path, float labelWidth, System.Action onPathChanged = null)
    {
        bool changed = false;
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label, GUILayout.Width(labelWidth));
        
        EditorGUI.BeginChangeCheck();
        path = EditorGUILayout.TextField(path);
        if (EditorGUI.EndChangeCheck())
        {
            changed = true;
            onPathChanged?.Invoke();
        }
        
        if (GUILayout.Button("浏览", GUILayout.Width(50)))
        {
            string selectedPath = EditorUtility.OpenFolderPanel("选择文件夹", path, "");
            if (!string.IsNullOrEmpty(selectedPath))
            {
                string newPath = GetRelativePath(selectedPath);
                if (newPath != path)
                {
                    path = newPath;
                    changed = true;
                    onPathChanged?.Invoke();
                }
            }
        }
        EditorGUILayout.EndHorizontal();
        
        // 拖拽区域
        Rect dropRect = EditorGUILayout.GetControlRect(false, 26);
        
        GUIStyle dropStyle = new GUIStyle(EditorStyles.helpBox)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 10
        };
        
        Event evt = Event.current;
        bool isDragTarget = dropRect.Contains(evt.mousePosition);
        
        if (isDragTarget && DragAndDrop.objectReferences.Length > 0)
        {
            dropStyle.normal.background = MakeTexture(2, 2, new Color(0.3f, 0.6f, 0.9f, 0.3f));
        }
        
        GUI.Box(dropRect, "📁 拖拽文件夹到这里", dropStyle);
        
        switch (evt.type)
        {
            case EventType.DragUpdated:
            case EventType.DragPerform:
                if (!dropRect.Contains(evt.mousePosition))
                    break;
                
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                
                if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    
                    foreach (Object draggedObject in DragAndDrop.objectReferences)
                    {
                        string draggedPath = AssetDatabase.GetAssetPath(draggedObject);
                        if (!string.IsNullOrEmpty(draggedPath))
                        {
                            if (Directory.Exists(draggedPath))
                            {
                                if (draggedPath != path)
                                {
                                    path = draggedPath;
                                    changed = true;
                                    onPathChanged?.Invoke();
                                }
                            }
                            else if (File.Exists(draggedPath))
                            {
                                string dirPath = Path.GetDirectoryName(draggedPath);
                                if (dirPath != path)
                                {
                                    path = dirPath;
                                    changed = true;
                                    onPathChanged?.Invoke();
                                }
                            }
                        }
                        break;
                    }
                }
                evt.Use();
                break;
        }
        
        return changed;
    }
    
    private Texture2D MakeTexture(int width, int height, Color color)
    {
        Color[] pixels = new Color[width * height];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = color;
        }
        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pixels);
        result.Apply();
        return result;
    }

    private void SetStatus(string message, MessageType type)
    {
        statusMessage = message;
        statusType = type;
        statusMessageTime = Time.realtimeSinceStartup;
    }

    private void ClearStatus()
    {
        statusMessage = "";
    }

    #endregion

    #region 数据类

    private class SpineAssetInfo
    {
        public string guid;
        public string path;
        public string name;
        public Object asset;
        public bool isAddressable;
        public string currentGroupName;
    }

    #endregion
}
