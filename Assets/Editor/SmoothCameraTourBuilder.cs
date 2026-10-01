using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class SmoothCameraTourBuilder
{
    private const string ScenePath = "Assets/Samples/VillageValley_Final.unity";
    private const string ClipPath = "Assets/TimeLine/SmoothCameraTour.anim";
    private const string ControllerPath = "Assets/TimeLine/SmoothCameraTour.controller";
    private const string RequestPath = "Assets/TimeLine/BUILD_SMOOTH_CAMERA_TOUR.request";

    static SmoothCameraTourBuilder()
    {
        EditorApplication.delayCall += BuildWhenRequested;
    }

    [MenuItem("Tools/Village Valley/Build Smooth Camera Tour")]
    public static void Build()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject movingCamera = GameObject.Find("CinemachineCamera");
        if (movingCamera == null)
            throw new System.InvalidOperationException("CinemachineCamera를 찾지 못했습니다.");

        GameObject vcam1 = GameObject.Find("Vcam1");
        GameObject vcam2 = GameObject.Find("Vcam2");
        GameObject vcam3 = GameObject.Find("Vacm3");
        if (vcam1 == null || vcam2 == null || vcam3 == null)
            throw new System.InvalidOperationException("카메라 투어 기준점(Vcam1/Vcam2/Vacm3)을 찾지 못했습니다.");

        Vector3[] positions =
        {
            vcam1.transform.position,
            vcam1.transform.position,
            vcam2.transform.position,
            vcam2.transform.position,
            movingCamera.transform.position,
            movingCamera.transform.position,
            vcam3.transform.position,
            vcam3.transform.position
        };
        Quaternion[] rotations =
        {
            vcam1.transform.rotation,
            vcam1.transform.rotation,
            vcam2.transform.rotation,
            vcam2.transform.rotation,
            movingCamera.transform.rotation,
            movingCamera.transform.rotation,
            vcam3.transform.rotation,
            vcam3.transform.rotation
        };
        float[] times = { 0f, 4f, 16f, 20f, 31f, 35f, 45f, 50f };

        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
        if (clip == null)
        {
            clip = new AnimationClip { name = "SmoothCameraTour", frameRate = 60f };
            AssetDatabase.CreateAsset(clip, ClipPath);
        }
        clip.ClearCurves();

        SetVectorCurve(clip, "m_LocalPosition", positions, times);
        SetQuaternionCurve(clip, "m_LocalRotation", rotations, times);
        clip.EnsureQuaternionContinuity();
        EditorUtility.SetDirty(clip);

        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        if (controller.layers.Length == 0)
            controller.AddLayer("Base Layer");
        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        foreach (ChildAnimatorState childState in stateMachine.states)
            stateMachine.RemoveState(childState.state);
        AnimatorState state = stateMachine.AddState("Smooth Camera Tour");
        state.motion = clip;
        stateMachine.defaultState = state;

        Animator animator = movingCamera.GetComponent<Animator>() ?? movingCamera.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.enabled = true;

        GameObject timeline = GameObject.Find("TimeLine");
        if (timeline != null)
            timeline.SetActive(false);
        foreach (PlayableDirector director in Object.FindObjectsByType<PlayableDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            director.enabled = false;

        vcam1.SetActive(false);
        vcam2.SetActive(false);
        vcam3.SetActive(false);
        movingCamera.SetActive(true);
        movingCamera.transform.SetPositionAndRotation(positions[0], rotations[0]);

        EditorUtility.SetDirty(movingCamera);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = movingCamera;
        Debug.Log("[SmoothCameraTour] 실제 이동 카메라 생성 완료: 0~50초");
    }

    private static void SetVectorCurve(AnimationClip clip, string property, Vector3[] values, float[] times)
    {
        SetCurve(clip, property + ".x", values, times, value => value.x);
        SetCurve(clip, property + ".y", values, times, value => value.y);
        SetCurve(clip, property + ".z", values, times, value => value.z);
    }

    private static void SetQuaternionCurve(AnimationClip clip, string property, Quaternion[] values, float[] times)
    {
        SetCurve(clip, property + ".x", values, times, value => value.x);
        SetCurve(clip, property + ".y", values, times, value => value.y);
        SetCurve(clip, property + ".z", values, times, value => value.z);
        SetCurve(clip, property + ".w", values, times, value => value.w);
    }

    private static void SetCurve<T>(AnimationClip clip, string property, T[] values, float[] times, System.Func<T, float> selector)
    {
        Keyframe[] keys = new Keyframe[times.Length];
        for (int index = 0; index < times.Length; index++)
            keys[index] = new Keyframe(times[index], selector(values[index]));
        AnimationCurve curve = new AnimationCurve(keys);
        for (int index = 0; index < curve.length; index++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, index, AnimationUtility.TangentMode.ClampedAuto);
            AnimationUtility.SetKeyRightTangentMode(curve, index, AnimationUtility.TangentMode.ClampedAuto);
        }
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(string.Empty, typeof(Transform), property), curve);
    }

    private static void BuildWhenRequested()
    {
        if (!File.Exists(RequestPath))
            return;
        try { Build(); }
        finally
        {
            if (File.Exists(RequestPath)) File.Delete(RequestPath);
            AssetDatabase.Refresh();
        }
    }
}
