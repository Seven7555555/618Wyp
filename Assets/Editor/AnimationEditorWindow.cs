using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Linq;

[InitializeOnLoad]
public class AnimationEditorWindow : EditorWindow
{
    private string folderPath = "Assets/Characters";
    private List<CharacterInfo> characterList = new List<CharacterInfo>();
    private int selectedCharacterIndex = -1;
    private string saveFilePath = "Assets/AnimationConfig.json";

    private float animationSpeed = 1.0f;
    private float animationLength = 10.0f;
    private bool isPlaying = false;
    private float playTime = 0.0f;

    [System.Serializable]
    public class CharacterInfo
    {
        public string characterName;
        public string characterPath;
        public List<AnimationClipInfo> animations = new List<AnimationClipInfo>();
    }

    [System.Serializable]
    public class AnimationClipInfo
    {
        public string clipName;
        public string clipPath;
        public float speed = 1.0f;
        public float length = 10.0f;
    }

    [MenuItem("编辑器/动画编辑器")]
    public static void OpenWindow()
    {
        GetWindow<AnimationEditorWindow>("动画编辑器");
    }

    private void OnEnable()
    {
        minSize = new Vector2(800, 600);
        LoadAnimationConfig();
    }

    private void OnGUI()
    {
        GUILayout.Label("动画编辑器", EditorStyles.boldLabel);
        GUILayout.Space(10);

        #region 资源加载区域
        EditorGUILayout.LabelField("角色资源加载", EditorStyles.boldLabel);
        folderPath = EditorGUILayout.TextField("角色资源文件夹路径", folderPath);
        if (GUILayout.Button("浏览", GUILayout.Width(100)))
        {
            folderPath = EditorUtility.OpenFolderPanel("选择角色资源文件夹", folderPath, "");
            if (!string.IsNullOrEmpty(folderPath) && folderPath.StartsWith(Application.dataPath))
            {
                folderPath = "Assets" + folderPath.Substring(Application.dataPath.Length);
            }
        }

        if (GUILayout.Button("加载角色资源", GUILayout.Width(200)))
        {
            LoadCharacterResources();
        }
        #endregion

        #region 角色列表区域
        if (characterList.Count > 0)
        {
            EditorGUILayout.LabelField("角色列表", EditorStyles.boldLabel);
            selectedCharacterIndex = EditorGUILayout.Popup(selectedCharacterIndex,
                characterList.Select(c => c.characterName).ToArray());

            if (selectedCharacterIndex >= 0)
            {
                DisplayCharacterAnimations();
            }
        }
        else
        {
            EditorGUILayout.LabelField("未加载角色资源，请先加载角色", EditorStyles.centeredGreyMiniLabel);
        }
        #endregion

        #region 动画操作区域
        if (selectedCharacterIndex >= 0 && characterList[selectedCharacterIndex].animations.Count > 0)
        {
            EditorGUILayout.LabelField("动画操作", EditorStyles.boldLabel);
            animationSpeed = EditorGUILayout.Slider("播放速度", animationSpeed, 0.1f, 5.0f);
            animationLength = EditorGUILayout.Slider("播放时长", animationLength, 1.0f, 30.0f);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("添加动画", GUILayout.Width(120)))
            {
                AddAnimation();
            }

            if (GUILayout.Button("删除动画", GUILayout.Width(120)))
            {
                DeleteAnimation();
            }

            isPlaying = GUILayout.Toggle(isPlaying, isPlaying ? "停止播放" : "播放动画", GUILayout.Width(120));
            EditorGUILayout.EndHorizontal();

            if (isPlaying)
            {
                playTime += Time.deltaTime * animationSpeed;
                if (playTime > animationLength)
                {
                    playTime = 0;
                }

                float progress = playTime / animationLength;
                Rect progressRect = GUILayoutUtility.GetRect(100, 20);
                EditorGUI.ProgressBar(progressRect, progress,
                    $"播放进度: {playTime / animationSpeed:F1}s / {animationLength:F1}s");
            }
            else
            {
                playTime = 0;
            }

            if (GUILayout.Button("保存动画配置", GUILayout.Width(200)))
            {
                SaveAnimationConfig();
            }
        }
        else if (selectedCharacterIndex >= 0)
        {
            EditorGUILayout.LabelField("该角色没有动画，请添加动画", EditorStyles.centeredGreyMiniLabel);
        }
        #endregion
    }

    private void LoadCharacterResources()
    {
        characterList.Clear();
        if (!Directory.Exists(Application.dataPath + folderPath.Substring("Assets".Length)))
        {
            EditorUtility.DisplayDialog("错误", "角色资源文件夹不存在", "确定");
            return;
        }

        string[] prefabPaths = Directory.GetFiles(
            Application.dataPath + folderPath.Substring("Assets".Length),
            "*.prefab",
            SearchOption.AllDirectories
        );

        foreach (string prefabPath in prefabPaths)
        {
            string assetPath = "Assets" + prefabPath.Substring(Application.dataPath.Length);
            GameObject characterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);

            if (characterPrefab != null)
            {
                CharacterInfo info = new CharacterInfo
                {
                    characterName = characterPrefab.name,
                    characterPath = assetPath
                };

                // 尝试从Animator控制器获取动画
                Animator animator = characterPrefab.GetComponent<Animator>();
                if (animator != null && animator.runtimeAnimatorController != null)
                {
                    AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
                    foreach (AnimationClip clip in clips)
                    {
                        info.animations.Add(new AnimationClipInfo
                        {
                            clipName = clip.name,
                            clipPath = AssetDatabase.GetAssetPath(clip),
                            speed = 1.0f,
                            length = clip.length
                        });
                    }
                }

                // 同时支持旧版Animation组件
                Animation animation = characterPrefab.GetComponent<Animation>();
                if (animation != null && animation.clip != null)
                {
                    info.animations.Add(new AnimationClipInfo
                    {
                        clipName = animation.clip.name,
                        clipPath = AssetDatabase.GetAssetPath(animation.clip),
                        speed = 1.0f,
                        length = animation.clip.length
                    });

                    // 添加额外动画
                    foreach (AnimationState state in animation)
                    {
                        if (state.clip != null && !info.animations.Exists(a => a.clipName == state.clip.name))
                        {
                            info.animations.Add(new AnimationClipInfo
                            {
                                clipName = state.clip.name,
                                clipPath = AssetDatabase.GetAssetPath(state.clip),
                                speed = 1.0f,
                                length = state.clip.length
                            });
                        }
                    }
                }

                if (info.animations.Count > 0)
                {
                    characterList.Add(info);
                }
            }
        }

        EditorUtility.DisplayDialog("成功", "已加载 " + characterList.Count + " 个角色资源", "确定");
    }

    private void DisplayCharacterAnimations()
    {
        EditorGUILayout.LabelField("动画列表", EditorStyles.boldLabel);
        CharacterInfo selectedCharacter = characterList[selectedCharacterIndex];

        if (selectedCharacter.animations.Count > 0)
        {
            for (int i = 0; i < selectedCharacter.animations.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(selectedCharacter.animations[i].clipName, GUILayout.Width(200));

                if (GUILayout.Button("编辑", GUILayout.Width(80)))
                {
                    animationSpeed = selectedCharacter.animations[i].speed;
                    animationLength = selectedCharacter.animations[i].length;
                }

                EditorGUILayout.EndHorizontal();
            }
        }
        else
        {
            EditorGUILayout.LabelField("没有动画", EditorStyles.centeredGreyMiniLabel);
        }
    }

    private void AddAnimation()
    {
        if (selectedCharacterIndex < 0) return;

        string[] filters = new string[]
        {
            "Animation Clips", "anim",
            "All Files", "*"
        };

        string selectedPath = EditorUtility.OpenFilePanelWithFilters(
            "选择动画片段",
            "",
            filters
        );

        if (!string.IsNullOrEmpty(selectedPath))
        {
            string[] animationPaths = { selectedPath };
            if (animationPaths.Length > 0)
            {
                string animationPath = animationPaths[0];
                if (animationPath.StartsWith(Application.dataPath))
                {
                    string assetPath = "Assets" + animationPath.Substring(Application.dataPath.Length);
                    AnimationClip animationClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);

                    if (animationClip != null)
                    {
                        CharacterInfo selectedCharacter = characterList[selectedCharacterIndex];
                        selectedCharacter.animations.Add(new AnimationClipInfo
                        {
                            clipName = animationClip.name,
                            clipPath = assetPath,
                            speed = animationSpeed,
                            length = animationLength
                        });

                        EditorUtility.DisplayDialog("成功", "已添加动画: " + animationClip.name, "确定");
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("错误", "选择的文件不是有效的 AnimationClip", "确定");
                    }
                }
                else
                {
                    EditorUtility.DisplayDialog("错误", "路径必须在 Unity 项目内", "确定");
                }
            }
        }
    }

    private void DeleteAnimation()
    {
        if (selectedCharacterIndex < 0) return;

        CharacterInfo selectedCharacter = characterList[selectedCharacterIndex];
        if (selectedCharacter.animations.Count == 0) return;

        int selectedAnimationIndex = EditorGUILayout.Popup(
            "选择要删除的动画",
            0,
            selectedCharacter.animations.Select(a => a.clipName).ToArray()
        );

        if (EditorUtility.DisplayDialog(
            "确认删除",
            "确定要删除动画: " + selectedCharacter.animations[selectedAnimationIndex].clipName + " 吗?",
            "确定",
            "取消"
        ))
        {
            selectedCharacter.animations.RemoveAt(selectedAnimationIndex);
            EditorUtility.DisplayDialog("成功", "已删除动画", "确定");
        }
    }

    private void SaveAnimationConfig()
    {
        if (selectedCharacterIndex < 0) return;

        CharacterInfo selectedCharacter = characterList[selectedCharacterIndex];
        for (int i = 0; i < selectedCharacter.animations.Count; i++)
        {
            selectedCharacter.animations[i].speed = animationSpeed;
            selectedCharacter.animations[i].length = animationLength;
        }

        try
        {
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(List<CharacterInfo>));
            using (MemoryStream ms = new MemoryStream())
            {
                serializer.WriteObject(ms, characterList);
                ms.Position = 0;
                using (StreamReader reader = new StreamReader(ms))
                {
                    string json = reader.ReadToEnd();
                    File.WriteAllText(Application.dataPath + saveFilePath.Substring("Assets".Length), json);
                    EditorUtility.DisplayDialog("成功", "动画配置已保存", "确定");
                }
            }
        }
        catch (System.Exception e)
        {
            EditorUtility.DisplayDialog("错误", "保存配置失败: " + e.Message, "确定");
        }
    }

    private void LoadAnimationConfig()
    {
        if (File.Exists(Application.dataPath + saveFilePath.Substring("Assets".Length)))
        {
            try
            {
                string json = File.ReadAllText(Application.dataPath + saveFilePath.Substring("Assets".Length));
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(List<CharacterInfo>));
                using (MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                {
                    characterList = (List<CharacterInfo>)serializer.ReadObject(ms);
                    EditorUtility.DisplayDialog("成功", "已加载动画配置", "确定");
                }
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("错误", "加载配置失败: " + e.Message, "确定");
            }
        }
    }
}