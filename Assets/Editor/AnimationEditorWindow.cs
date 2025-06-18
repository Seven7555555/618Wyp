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

    [MenuItem("�༭��/�����༭��")]
    public static void OpenWindow()
    {
        GetWindow<AnimationEditorWindow>("�����༭��");
    }

    private void OnEnable()
    {
        minSize = new Vector2(800, 600);
        LoadAnimationConfig();
    }

    private void OnGUI()
    {
        GUILayout.Label("�����༭��", EditorStyles.boldLabel);
        GUILayout.Space(10);

        #region ��Դ��������
        EditorGUILayout.LabelField("��ɫ��Դ����", EditorStyles.boldLabel);
        folderPath = EditorGUILayout.TextField("��ɫ��Դ�ļ���·��", folderPath);
        if (GUILayout.Button("���", GUILayout.Width(100)))
        {
            folderPath = EditorUtility.OpenFolderPanel("ѡ���ɫ��Դ�ļ���", folderPath, "");
            if (!string.IsNullOrEmpty(folderPath) && folderPath.StartsWith(Application.dataPath))
            {
                folderPath = "Assets" + folderPath.Substring(Application.dataPath.Length);
            }
        }

        if (GUILayout.Button("���ؽ�ɫ��Դ", GUILayout.Width(200)))
        {
            LoadCharacterResources();
        }
        #endregion

        #region ��ɫ�б�����
        if (characterList.Count > 0)
        {
            EditorGUILayout.LabelField("��ɫ�б�", EditorStyles.boldLabel);
            selectedCharacterIndex = EditorGUILayout.Popup(selectedCharacterIndex,
                characterList.Select(c => c.characterName).ToArray());

            if (selectedCharacterIndex >= 0)
            {
                DisplayCharacterAnimations();
            }
        }
        else
        {
            EditorGUILayout.LabelField("δ���ؽ�ɫ��Դ�����ȼ��ؽ�ɫ", EditorStyles.centeredGreyMiniLabel);
        }
        #endregion

        #region ������������
        if (selectedCharacterIndex >= 0 && characterList[selectedCharacterIndex].animations.Count > 0)
        {
            EditorGUILayout.LabelField("��������", EditorStyles.boldLabel);
            animationSpeed = EditorGUILayout.Slider("�����ٶ�", animationSpeed, 0.1f, 5.0f);
            animationLength = EditorGUILayout.Slider("����ʱ��", animationLength, 1.0f, 30.0f);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("��Ӷ���", GUILayout.Width(120)))
            {
                AddAnimation();
            }

            if (GUILayout.Button("ɾ������", GUILayout.Width(120)))
            {
                DeleteAnimation();
            }

            isPlaying = GUILayout.Toggle(isPlaying, isPlaying ? "ֹͣ����" : "���Ŷ���", GUILayout.Width(120));
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
                    $"���Ž���: {playTime / animationSpeed:F1}s / {animationLength:F1}s");
            }
            else
            {
                playTime = 0;
            }

            if (GUILayout.Button("���涯������", GUILayout.Width(200)))
            {
                SaveAnimationConfig();
            }
        }
        else if (selectedCharacterIndex >= 0)
        {
            EditorGUILayout.LabelField("�ý�ɫû�ж���������Ӷ���", EditorStyles.centeredGreyMiniLabel);
        }
        #endregion
    }

    private void LoadCharacterResources()
    {
        characterList.Clear();
        if (!Directory.Exists(Application.dataPath + folderPath.Substring("Assets".Length)))
        {
            EditorUtility.DisplayDialog("����", "��ɫ��Դ�ļ��в�����", "ȷ��");
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

                // ���Դ�Animator��������ȡ����
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

                // ͬʱ֧�־ɰ�Animation���
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

                    // ��Ӷ��⶯��
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

        EditorUtility.DisplayDialog("�ɹ�", "�Ѽ��� " + characterList.Count + " ����ɫ��Դ", "ȷ��");
    }

    private void DisplayCharacterAnimations()
    {
        EditorGUILayout.LabelField("�����б�", EditorStyles.boldLabel);
        CharacterInfo selectedCharacter = characterList[selectedCharacterIndex];

        if (selectedCharacter.animations.Count > 0)
        {
            for (int i = 0; i < selectedCharacter.animations.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(selectedCharacter.animations[i].clipName, GUILayout.Width(200));

                if (GUILayout.Button("�༭", GUILayout.Width(80)))
                {
                    animationSpeed = selectedCharacter.animations[i].speed;
                    animationLength = selectedCharacter.animations[i].length;
                }

                EditorGUILayout.EndHorizontal();
            }
        }
        else
        {
            EditorGUILayout.LabelField("û�ж���", EditorStyles.centeredGreyMiniLabel);
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
            "ѡ�񶯻�Ƭ��",
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

                        EditorUtility.DisplayDialog("�ɹ�", "����Ӷ���: " + animationClip.name, "ȷ��");
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("����", "ѡ����ļ�������Ч�� AnimationClip", "ȷ��");
                    }
                }
                else
                {
                    EditorUtility.DisplayDialog("����", "·�������� Unity ��Ŀ��", "ȷ��");
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
            "ѡ��Ҫɾ���Ķ���",
            0,
            selectedCharacter.animations.Select(a => a.clipName).ToArray()
        );

        if (EditorUtility.DisplayDialog(
            "ȷ��ɾ��",
            "ȷ��Ҫɾ������: " + selectedCharacter.animations[selectedAnimationIndex].clipName + " ��?",
            "ȷ��",
            "ȡ��"
        ))
        {
            selectedCharacter.animations.RemoveAt(selectedAnimationIndex);
            EditorUtility.DisplayDialog("�ɹ�", "��ɾ������", "ȷ��");
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
                    EditorUtility.DisplayDialog("�ɹ�", "���������ѱ���", "ȷ��");
                }
            }
        }
        catch (System.Exception e)
        {
            EditorUtility.DisplayDialog("����", "��������ʧ��: " + e.Message, "ȷ��");
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
                    EditorUtility.DisplayDialog("�ɹ�", "�Ѽ��ض�������", "ȷ��");
                }
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("����", "��������ʧ��: " + e.Message, "ȷ��");
            }
        }
    }
}