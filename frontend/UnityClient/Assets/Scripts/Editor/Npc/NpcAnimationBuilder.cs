using System;
using System.Linq;
using A305.EditorTools.Common;
using A305.EditorTools.Player;
using A305.Npc;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace A305.EditorTools.Npc
{
    /// <summary>
    /// NPC 생활 동작을 준비한다. Quaternius Universal Animation Library(CC0) FBX를 Humanoid로 가져와 리틀멍 NPC에 리타겟팅하고,
    /// 이동 동작에 일하기·물건 집기·말하기·앉기 상태를 더한 NPC 전용 Animator Controller를 만든다. 상태 번호는 <see cref="NpcAnimation"/>과 같다.
    /// 컨트롤러가 이미 있으면 그대로 쓰므로, 구성을 바꾸려면 컨트롤러를 지우고 다시 실행한다.
    /// </summary>
    public static class NpcAnimationBuilder
    {
        private const string MotionPath =
            "Assets/ThirdParty/QuaterniusUniversalAnimation/UAL1_Standard.fbx";
        private const string Folder = "Assets/Art/Characters/Npc";
        private const string ControllerPath = Folder + "/NpcLife.controller";

        private static readonly (string Clip, NpcAnimation Animation)[] ActionClips =
        {
            ("Interact", NpcAnimation.Work),
            ("PickUp_Table", NpcAnimation.PickUp),
            ("Idle_Talking_Loop", NpcAnimation.Talk),
            ("Sitting_Idle_Loop", NpcAnimation.Sit),
        };

        /// <summary>NPC 전용 컨트롤러. 없으면 동작 클립을 가져오고 만든다.</summary>
        public static RuntimeAnimatorController PrepareController()
        {
            var existing = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            if (existing != null)
                return existing;

            ImportClips();
            AnimationClip[] clips = AssetDatabase
                .LoadAllAssetsAtPath(MotionPath)
                .OfType<AnimationClip>()
                .ToArray();
            AnimationClip Clip(string name) => clips.First(clip => clip.name == name);

            BuilderTools.EnsureFolder(Folder);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            // 이동 파라미터는 CharacterAnimationDriver가 플레이어와 같이 넘긴다.
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
            controller.AddParameter("VerticalSpeed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Action", AnimatorControllerParameterType.Int);
            AnimatorControllerParameter[] parameters = controller.parameters;
            parameters[1].defaultBool = true;
            controller.parameters = parameters;

            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState locomotion = controller.CreateBlendTreeInController(
                "Locomotion",
                out BlendTree tree
            );
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            // 서 있기·걷기·뛰기는 플레이어와 같은 리틀멍 이동 클립과 속도 기준을 쓴다. 대기 자세가 같아야 NPC 키(서비스 높이)도 그대로다.
            tree.children = PlayerLocomotion().children;
            machine.defaultState = locomotion;

            foreach ((string clipName, NpcAnimation animation) in ActionClips)
            {
                AnimatorState state = machine.AddState(animation.ToString());
                state.motion = Clip(clipName);
                AnimatorStateTransition enter = machine.AddAnyStateTransition(state);
                Setup(enter);
                enter.canTransitionToSelf = false;
                enter.AddCondition(AnimatorConditionMode.Equals, (int)animation, "Action");
                AnimatorStateTransition leave = state.AddTransition(locomotion);
                Setup(leave);
                leave.AddCondition(AnimatorConditionMode.Equals, (int)NpcAnimation.None, "Action");
            }

            AssetDatabase.SaveAssets();
            return controller;
        }

        /// <summary>
        /// NPC 생활에 동작 컨트롤러를 연결한다. 장면에 저장된 모델 Animator는 그대로 두고, 생활이 켜질 때 바꿔 끼운다
        /// (<see cref="NpcRoutine"/>). Ben처럼 전용 Generic 뼈대를 쓰는 모델은 자기 컨트롤러를 유지한다.
        /// </summary>
        public static void Connect(NpcRoutine routine, RuntimeAnimatorController controller)
        {
            BuilderTools.SetReference(routine, "lifeAnimator", controller);
        }

        // Quaternius 안내대로 Humanoid·축 변환으로 가져오고, 쓰는 동작만 남겨 이용하는 동안 되풀이한다.
        // 위치는 NPC가 코드로 움직이므로 루트 회전·높이·위치를 동작에 맡기지 않는다.
        private static void ImportClips()
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(MotionPath);
            if (importer == null)
                throw new InvalidOperationException("NPC 동작 FBX가 없습니다: " + MotionPath);
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.bakeAxisConversion = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.SaveAndReimport();

            string[] used = ActionClips.Select(entry => entry.Clip).ToArray();
            ModelImporterClipAnimation[] clips = importer
                .defaultClipAnimations.Where(clip => used.Contains(ShortName(clip.name)))
                .ToArray();
            foreach (ModelImporterClipAnimation clip in clips)
            {
                clip.name = ShortName(clip.name);
                clip.loopTime = true;
                clip.loopPose = true;
                clip.lockRootRotation = true;
                clip.lockRootHeightY = true;
                clip.lockRootPositionXZ = true;
                clip.keepOriginalOrientation = true;
                clip.heightFromFeet = true;
            }

            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        // FBX 안의 동작 이름은 "Armature|Interact"처럼 뼈대 이름이 앞에 붙는다.
        private static string ShortName(string clipName)
        {
            return clipName.Substring(clipName.LastIndexOf('|') + 1);
        }

        private static BlendTree PlayerLocomotion()
        {
            var player = (AnimatorController)LittleMungAnimationBuilder.Build();
            return (BlendTree)
                player
                    .layers[0]
                    .stateMachine.states.First(child => child.state.name == "Locomotion")
                    .state.motion;
        }

        private static void Setup(AnimatorStateTransition transition)
        {
            transition.hasExitTime = false;
            transition.hasFixedDuration = true;
            transition.duration = .2f;
        }
    }
}
