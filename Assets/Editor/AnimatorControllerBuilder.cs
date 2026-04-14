using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.IO;

public class AnimatorControllerBuilder
{
    private const string ANIM_PATH = "Assets/Animations/Player/";
    private const string CONTROLLER_PATH = "Assets/Animations/Player/PlayerAnimator.controller";

    [MenuItem("Tools/Build Player Animator")]
    public static void BuildAnimatorController()
    {
        // Borrar el anterior si existe
        if (File.Exists(CONTROLLER_PATH))
        {
            AssetDatabase.DeleteAsset(CONTROLLER_PATH);
        }

        var controller = AnimatorController.CreateAnimatorControllerAtPath(CONTROLLER_PATH);

        // --- PARAMETROS ---
        controller.AddParameter("MoveX", AnimatorControllerParameterType.Float);
        controller.AddParameter("MoveZ", AnimatorControllerParameterType.Float);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsJumping", AnimatorControllerParameterType.Bool);

        var rootStateMachine = controller.layers[0].stateMachine;

        // --- BLEND TREE DE MOVIMIENTO EN SUELO ---
        BlendTree groundBlendTree;
        var groundState = controller.CreateBlendTreeInController("Ground Movement", out groundBlendTree, 0);
        groundBlendTree.blendType = BlendTreeType.FreeformDirectional2D;
        groundBlendTree.blendParameter = "MoveX";
        groundBlendTree.blendParameterY = "MoveZ";

        // Cargar animaciones
        var idle = LoadAnim("Idle");
        var walk = LoadAnim("Walk");
        var run = LoadAnim("Run");
        var walkBack = LoadAnim("WalkBack");
        var leftStrafe = LoadAnim("LeftStrafe");
        var rightStrafe = LoadAnim("RightStrafe");

        // Posiciones en el blend tree
        groundBlendTree.AddChild(idle, new Vector2(0, 0));
        groundBlendTree.AddChild(walk, new Vector2(0, 0.5f));
        groundBlendTree.AddChild(run, new Vector2(0, 1f));
        groundBlendTree.AddChild(walkBack, new Vector2(0, -0.5f));
        groundBlendTree.AddChild(leftStrafe, new Vector2(-0.5f, 0));    // LeftStrafe real
        groundBlendTree.AddChild(rightStrafe, new Vector2(0.5f, 0));    // RightStrafe real

        rootStateMachine.defaultState = groundState;

        // --- ESTADOS DE SALTO (SIMPLIFICADO: sin Landing) ---
        var jumpUpState = rootStateMachine.AddState("JumpUp", new Vector3(500, 0, 0));
        jumpUpState.motion = LoadAnim("JumpingUp");
        jumpUpState.speed = 1.3f;

        var fallingState = rootStateMachine.AddState("Falling", new Vector3(500, 80, 0));
        fallingState.motion = LoadAnim("FallingIdle");

        // --- TRANSICIONES ---

        // Ground -> JumpUp (cuando salta)
        var toJump = groundState.AddTransition(jumpUpState);
        toJump.AddCondition(AnimatorConditionMode.If, 0, "IsJumping");
        toJump.duration = 0.05f;
        toJump.hasExitTime = false;
        toJump.hasFixedDuration = true;

        // JumpUp -> Falling (cuando termina la anim de salto)
        var toFalling = jumpUpState.AddTransition(fallingState);
        toFalling.hasExitTime = true;
        toFalling.exitTime = 0.85f;
        toFalling.duration = 0.1f;
        toFalling.hasFixedDuration = true;

        // Falling -> Ground (cuando toca el suelo, directo sin Landing)
        var toGround = fallingState.AddTransition(groundState);
        toGround.AddCondition(AnimatorConditionMode.If, 0, "IsGrounded");
        toGround.duration = 0.1f;
        toGround.hasExitTime = false;
        toGround.hasFixedDuration = true;

        // Seguridad: JumpUp -> Ground (si aterriza muy rapido, salto chiquito)
        var jumpToGround = jumpUpState.AddTransition(groundState);
        jumpToGround.AddCondition(AnimatorConditionMode.If, 0, "IsGrounded");
        jumpToGround.AddCondition(AnimatorConditionMode.IfNot, 0, "IsJumping");
        jumpToGround.duration = 0.1f;
        jumpToGround.hasExitTime = false;
        jumpToGround.hasFixedDuration = true;

        // Guardar
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("PlayerAnimator reconstruido! (Sin Landing, con LeftStrafe restaurado)");
    }

    private static AnimationClip LoadAnim(string name)
    {
        string path = ANIM_PATH + name + ".anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
        {
            Debug.LogWarning($"No se encontro la animacion: {path}");
        }
        return clip;
    }
}
