using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Contents.FallenCommander
{
    [DisallowMultipleComponent]
    public sealed class FallenCommanderBossDeathPresentation : MonoBehaviour
    {
        private FallenCommanderBossAnimationPresenter animationPresenter;

        public static FallenCommanderBossDeathPresentation Create(
            UnitActor source,
            Transform parent)
        {
            if (source == null)
            {
                return null;
            }

            var clone = Instantiate(
                source.gameObject,
                source.transform.position,
                source.transform.rotation,
                parent);
            clone.name = source.name + " [Death Presentation]";

            var presentation = clone.GetComponent<FallenCommanderBossDeathPresentation>();
            if (presentation == null)
            {
                presentation = clone.AddComponent<FallenCommanderBossDeathPresentation>();
            }

            presentation.Initialize();
            return presentation;
        }

        public void Play(AnimationClip motion, float duration)
        {
            if (animationPresenter == null || motion == null)
            {
                return;
            }

            animationPresenter.Configure(transform);
            animationPresenter.Play(
                motion,
                stopAfterMotion: true,
                durationOverride: duration);
        }

        public void Release()
        {
            if (animationPresenter != null)
            {
                animationPresenter.Stop();
            }

            if (gameObject != null)
            {
                Destroy(gameObject);
            }
        }

        private void Initialize()
        {
            var behaviours = GetComponentsInChildren<Behaviour>(true);
            foreach (var behaviour in behaviours)
            {
                if (behaviour is Animator || behaviour == this)
                {
                    behaviour.enabled = true;
                    continue;
                }

                behaviour.enabled = false;
            }

            var colliders = GetComponentsInChildren<Collider>(true);
            foreach (var collider in colliders)
            {
                collider.enabled = false;
            }

            animationPresenter = GetComponent<FallenCommanderBossAnimationPresenter>();
            if (animationPresenter == null)
            {
                animationPresenter = gameObject.AddComponent<FallenCommanderBossAnimationPresenter>();
            }

            animationPresenter.enabled = true;
        }
    }
}
