using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class Ch09CharacterBuilder
{
    private const string Folder = "Assets/Characters/Ch09";
    private const string ModelPath = Folder + "/Ch09_nonPBR.fbx";
    private const string AnimationPath = Folder + "/Nervously_Look_Around.fbx";
    private const string ControllerPath = Folder + "/Ch09_Nervous.controller";
    private const string PrefabPath = Folder + "/Ch09_Nervous.prefab";
    private const string RequestPath = Folder + "/BUILD.request";
    private const string ApplyRequestPath = Folder + "/APPLY_COLOR_MOTION.request";
    private const string DeleteAkaiRequestPath = Folder + "/DELETE_AKAI.request";
    private const string MainScenePath = "Assets/Samples/VillageValley_Final.unity";
    private const string MaterialFolder = Folder + "/Materials";
    private const string TextureFolder = Folder + "/Textures";

    static Ch09CharacterBuilder()
    {
        EditorApplication.delayCall += BuildWhenRequested;
    }

    [MenuItem("Tools/Ch09/Build Nervous Character")]
    public static void Build()
    {
        try
        {
            ConfigureModel();
            AssetDatabase.Refresh();

            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath)
                .OfType<Avatar>()
                .FirstOrDefault(a => a.isValid && a.isHuman);
            if (avatar == null)
                throw new System.InvalidOperationException("Ch09 모델에서 유효한 Humanoid Avatar를 만들지 못했습니다.");

            ConfigureAnimation(avatar);
            AssetDatabase.Refresh();

            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(AnimationPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            if (clip == null)
                throw new System.InvalidOperationException("애니메이션 FBX에서 AnimationClip을 찾지 못했습니다.");

            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
                AssetDatabase.DeleteAsset(ControllerPath);

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            AnimatorState state = controller.layers[0].stateMachine.AddState("Nervously Look Around");
            state.motion = clip;
            controller.layers[0].stateMachine.defaultState = state;

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.name = "Ch09_Nervous";
            Animator animator = instance.GetComponent<Animator>() ?? instance.AddComponent<Animator>();
            animator.avatar = avatar;
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;

            PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
            Object.DestroyImmediate(instance);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            EditorGUIUtility.PingObject(Selection.activeObject);
            Debug.Log($"[Ch09 Builder] 완료: {PrefabPath} / Clip: {clip.name}");
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
        }
        finally
        {
            if (File.Exists(RequestPath))
                File.Delete(RequestPath);
            AssetDatabase.Refresh();
        }
    }

    [MenuItem("Tools/Ch09/Place Beside Player")]
    public static void PlaceBesidePlayer()
    {
        Scene scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
        GameObject player = GameObject.Find("Akai") ?? GameObject.Find("CmCharacter") ?? GameObject.FindWithTag("Player");
        if (player == null)
            throw new System.InvalidOperationException("씬에서 플레이어 오브젝트를 찾지 못했습니다.");

        GameObject existing = GameObject.Find("Ch09_Nervous_BesidePlayer");
        if (existing != null)
            Object.DestroyImmediate(existing);

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
            throw new FileNotFoundException("먼저 Ch09 프리팹을 생성해야 합니다.", PrefabPath);

        GameObject character = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        character.name = "Ch09_Nervous_BesidePlayer";
        character.transform.SetPositionAndRotation(
            player.transform.TransformPoint(new Vector3(1.5f, 0f, 0f)),
            player.transform.rotation);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = character;
        Debug.Log($"[Ch09 Builder] 배치 완료: {player.name} 오른쪽 1.5m / {MainScenePath}");
    }

    [MenuItem("Tools/Ch09/Apply Color And Motion")]
    public static void ApplyColorAndMotion()
    {
        Directory.CreateDirectory(MaterialFolder);
        Directory.CreateDirectory(TextureFolder);
        AssetDatabase.Refresh();

        ModelImporter importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (importer == null)
            throw new FileNotFoundException("캐릭터 FBX를 찾지 못했습니다.", ModelPath);

        importer.ExtractTextures(TextureFolder);
        ExtractEmbeddedMaterials();
        AssetDatabase.Refresh();

        Shader pipelineShader = Shader.Find("Universal Render Pipeline/Lit")
            ?? Shader.Find("HDRP/Lit")
            ?? Shader.Find("Standard");
        Texture2D diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + "/Ch09_1001_Diffuse.png");
        Texture2D normalTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + "/Ch09_1001_Normal.png");

        TextureImporter normalImporter = AssetImporter.GetAtPath(TextureFolder + "/Ch09_1001_Normal.png") as TextureImporter;
        if (normalImporter != null && normalImporter.textureType != TextureImporterType.NormalMap)
        {
            normalImporter.textureType = TextureImporterType.NormalMap;
            normalImporter.SaveAndReimport();
            normalTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + "/Ch09_1001_Normal.png");
        }

        foreach (string materialGuid in AssetDatabase.FindAssets("t:Material", new[] { MaterialFolder }))
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(materialGuid));
            if (material == null || pipelineShader == null)
                continue;

            Texture baseTexture = material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap") : null;
            if (baseTexture == null && material.HasProperty("_MainTex"))
                baseTexture = material.GetTexture("_MainTex");
            if (baseTexture == null)
                baseTexture = diffuseTexture;
            Color baseColor = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor")
                : material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;

            material.shader = pipelineShader;
            if (material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", baseTexture);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", baseColor);
            if (material.HasProperty("_BumpMap") && normalTexture != null)
            {
                material.SetTexture("_BumpMap", normalTexture);
                material.EnableKeyword("_NORMALMAP");
            }
            EditorUtility.SetDirty(material);
        }

        Scene scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
        GameObject character = GameObject.Find("Ch09_Nervous_BesidePlayer");
        if (character == null)
            throw new System.InvalidOperationException("Ch09_Nervous_BesidePlayer를 씬에서 찾지 못했습니다.");

        Animator animator = character.GetComponent<Animator>() ?? character.AddComponent<Animator>();
        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
        animator.avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().FirstOrDefault(a => a.isValid && a.isHuman);
        animator.applyRootMotion = false;
        animator.enabled = true;
        animator.speed = 1f;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        EditorUtility.SetDirty(character);
        EditorUtility.SetDirty(animator);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Ch09 Builder] 색상/모션 적용 완료: {character.name}, Materials={AssetDatabase.FindAssets("t:Material", new[] { MaterialFolder }).Length}");
    }

    private static void ExtractEmbeddedMaterials()
    {
        foreach (Material material in AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Material>().ToArray())
        {
            if (!AssetDatabase.IsSubAsset(material))
                continue;

            string safeName = string.Concat(material.name.Select(character =>
                Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
            string destination = AssetDatabase.GenerateUniqueAssetPath($"{MaterialFolder}/{safeName}.mat");
            string error = AssetDatabase.ExtractAsset(material, destination);
            if (!string.IsNullOrEmpty(error))
                Debug.LogWarning($"[Ch09 Builder] 머티리얼 추출 실패 ({material.name}): {error}");
        }
    }

    private static void BuildWhenRequested()
    {
        if (File.Exists(RequestPath))
            Build();
        if (File.Exists(ApplyRequestPath))
        {
            try
            {
                ApplyColorAndMotion();
            }
            finally
            {
                if (File.Exists(ApplyRequestPath))
                    File.Delete(ApplyRequestPath);
                AssetDatabase.Refresh();
            }
        }
        if (File.Exists(DeleteAkaiRequestPath))
        {
            try
            {
                DeleteAkai();
            }
            finally
            {
                if (File.Exists(DeleteAkaiRequestPath))
                    File.Delete(DeleteAkaiRequestPath);
                AssetDatabase.Refresh();
            }
        }
    }

    [MenuItem("Tools/Ch09/Delete Akai")]
    public static void DeleteAkai()
    {
        Scene scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
        GameObject akai = GameObject.Find("Akai");
        if (akai == null)
        {
            Debug.LogWarning("[Ch09 Builder] Akai가 이미 삭제되어 있습니다.");
            return;
        }

        Object.DestroyImmediate(akai);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[Ch09 Builder] Akai 삭제 완료: {MainScenePath}");
    }

    private static void ConfigureModel()
    {
        ModelImporter importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (importer == null)
            throw new FileNotFoundException("캐릭터 FBX를 찾지 못했습니다.", ModelPath);

        importer.importAnimation = true;
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.SaveAndReimport();
    }

    private static void ConfigureAnimation(Avatar avatar)
    {
        ModelImporter importer = AssetImporter.GetAtPath(AnimationPath) as ModelImporter;
        if (importer == null)
            throw new FileNotFoundException("애니메이션 FBX를 찾지 못했습니다.", AnimationPath);

        importer.importAnimation = true;
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
        importer.sourceAvatar = avatar;

        ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
        foreach (ModelImporterClipAnimation clip in clips)
        {
            clip.loopTime = true;
            clip.loopPose = true;
        }
        importer.clipAnimations = clips;
        importer.SaveAndReimport();
    }
}
