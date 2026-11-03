using System.Collections.Generic;
using UnityEngine;

namespace A305.Npc
{
    /// <summary>
    /// 생활 동작 컨트롤러(<see cref="NpcRoutine"/>)로 바꿔 끼울 때 모델이 쓰던 캐릭터 전용 클립을 이어받는다.
    /// 예: 마스터 골격 캐릭터는 쇄골을 편 자기 Idle을 공용 Idle 대신 쓴다.
    /// </summary>
    public static class NpcLifeAnimator
    {
        public static RuntimeAnimatorController KeepCharacterClips(
            RuntimeAnimatorController life,
            RuntimeAnimatorController current
        )
        {
            if (current is not AnimatorOverrideController character)
            {
                return life;
            }

            var characterClips = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            character.GetOverrides(characterClips);
            var lifeClips = new HashSet<AnimationClip>(life.animationClips);
            var kept = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            foreach (KeyValuePair<AnimationClip, AnimationClip> pair in characterClips)
            {
                if (pair.Value != null && lifeClips.Contains(pair.Key))
                {
                    kept.Add(pair);
                }
            }

            if (kept.Count == 0)
            {
                return life;
            }

            var combined = new AnimatorOverrideController(life)
            {
                name = $"{life.name} ({character.name})",
            };
            combined.ApplyOverrides(kept);
            return combined;
        }
    }
}
