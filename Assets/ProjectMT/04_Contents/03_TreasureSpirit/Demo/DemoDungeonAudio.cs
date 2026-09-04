using ProjectMT.Shared.Audio;
using UnityEngine;
using UnityEngine.Audio;

namespace ProjectMT.Contents.TreasureSpirit.Demo
{
    [DisallowMultipleComponent]
    public sealed class DemoDungeonAudio : MonoBehaviour
    {
        public static DemoDungeonAudio Active { get; private set; }

        private const float MasterVolume = 0.4f;
        private const float SfxVolumeScale = 0.5f;
        private const float HearRadius = 5f;
        private const int OneShotVoiceCount = 8;
        private const float BgmVolume = 0.32f;
        private const float DungeonAmbienceVolume = 0.26f;
        private const float WindAmbienceVolume = 0.16f;
        private const float TorchAmbienceVolume = 0.2f;
        private const float FireLoopVolume = 1f;
        private const float SawLoopVolume = 1f;
        private const float ArrowVolume = 2f;
        private const string FireLoopKey = "Fire";
        private const string SawLoopKey = "Saw";

        [Header("출력")]
        [SerializeField] private AudioMixerGroup bgmGroup;
        [SerializeField] private AudioMixerGroup sfxGroup;

        [Header("BGM / 앰비언스")]
        [SerializeField] private AudioClip bgm;
        [SerializeField] private AudioClip dungeonAmbience;
        [SerializeField] private AudioClip windAmbience;
        [SerializeField] private AudioClip torchAmbience;

        [Header("SFX")]
        [SerializeField] private AudioClip jumpSfx;
        [SerializeField] private AudioClip chestOpenSfx;
        [SerializeField] private AudioClip quizUiSfx;
        [SerializeField] private AudioClip keySfx;
        [SerializeField] private AudioClip collectSfx;
        [SerializeField] private AudioClip doorSfx;
        [SerializeField] private AudioClip prisonDoorSfx;
        [SerializeField] private AudioClip lockFailSfx;
        [SerializeField] private AudioClip fireIgniteSfx;
        [SerializeField] private AudioClip fireLoopSfx;
        [SerializeField] private AudioClip spikeSfx;
        [SerializeField] private AudioClip sawLoopSfx;
        [SerializeField] private AudioClip arrowSfx;
        [SerializeField] private AudioClip mimicSfx;
        [SerializeField] private AudioClip followerAttackSfx;
        [SerializeField] private AudioClip guardAttackSfx;
        [SerializeField] private AudioClip clearSfx;
        [SerializeField] private AudioClip failSfx;
        [SerializeField] private AudioClip commanderDamageSfx;

        private Bed[] beds;
        private AudioSource[] oneShotVoices;
        private AudioSource uiSource;
        private int oneShotIndex;
        private Transform listener;
        private bool bedsPlaying;
        private readonly LoopVoice[] loops = new LoopVoice[16];
        private int loopCount;

        private void Awake()
        {
            Active = this;
            BuildOneShotVoices();
            uiSource = CreateSource("UiSfx", sfxGroup, false, 0f);
            beds = new[]
            {
                CreateBed("Bgm", bgmGroup, bgm, BgmVolume, true),
                CreateBed("DungeonAmbience", sfxGroup, dungeonAmbience, DungeonAmbienceVolume, false),
                CreateBed("WindAmbience", sfxGroup, windAmbience, WindAmbienceVolume, false),
                CreateBed("TorchAmbience", sfxGroup, torchAmbience, TorchAmbienceVolume, false)
            };
        }

        private void OnEnable()
        {
            Active = this;
            AudioRuntimeSettings.Changed += ApplyBedVolumes;
        }

        private void OnDisable()
        {
            AudioRuntimeSettings.Changed -= ApplyBedVolumes;
            if (Active == this)
            {
                Active = null;
            }

            StopBeds();
            StopAllLoops();
        }

        public void StartBeds()
        {
            listener = null;
            Transform player = DemoDungeonController.Active != null ? DemoDungeonController.Active.PlayerTransform : null;
            if (player != null)
            {
                listener = player;
            }

            bedsPlaying = true;
            for (int i = 0; i < beds.Length; i++)
            {
                PlayBed(beds[i]);
            }
        }

        public void StopBeds()
        {
            bedsPlaying = false;
            listener = null;
            if (beds == null)
            {
                return;
            }

            for (int i = 0; i < beds.Length; i++)
            {
                AudioSource source = beds[i].Source;
                if (source != null && source.isPlaying)
                {
                    source.Stop();
                }
            }
        }

        public static void PlayJump(Vector3 position)
        {
            Active?.Play(Active.jumpSfx, position, 0.7f, true, 0.08f);
        }

        public static void PlayCommanderDamage(Vector3 position)
        {
            Active?.Play(Active.commanderDamageSfx, position, 1f, false, 0.25f);
        }

        public static void PlayChestOpen(Vector3 position)
        {
            Active?.Play(Active.chestOpenSfx, position, 0.85f, true, 0.04f);
        }

        public static void PlayQuizUi()
        {
            Active?.Play(Active.quizUiSfx, Vector3.zero, 0.55f, false, 0.03f);
        }

        public static void PlayKey(Vector3 position)
        {
            Active?.Play(Active.keySfx, position, 0.85f, true, 0.05f);
            Active?.Play(Active.collectSfx, position, 0.65f, false, 0f);
        }

        public static void PlayDoor(Vector3 position)
        {
            Active?.Play(Active.doorSfx, position, 0.8f, true, 0.06f);
        }

        public static void PlayPrisonDoor(Vector3 position)
        {
            Active?.Play(Active.prisonDoorSfx, position, 0.85f, true, 0.04f);
        }

        public static void PlayLockFail(Vector3 position)
        {
            Active?.Play(Active.lockFailSfx, position, 0.7f, true, 0.04f);
        }

        public static void PlayFireIgnite(Vector3 position)
        {
            Active?.Play(Active.fireIgniteSfx, position, 1f, true, 0.05f);
        }

        public static void PlaySpike(Vector3 position)
        {
            Active?.Play(Active.spikeSfx, position, 0.75f, true, 0.06f);
        }

        public static void PlayArrow(Vector3 position)
        {
            Active?.Play(Active.arrowSfx, position, ArrowVolume, true, 0.06f);
        }

        public static void PlayMimic(Vector3 position)
        {
            Active?.Play(Active.mimicSfx, position, 0.9f, true, 0.05f);
        }

        public static void PlayFollowerAttack(Vector3 position)
        {
            Active?.Play(Active.followerAttackSfx, position, 0.95f, true, 0.07f);
        }

        public static void PlayGuardAttack(Vector3 position)
        {
            Active?.Play(Active.guardAttackSfx, position, 0.9f, true, 0.06f);
        }

        public static void PlayClear()
        {
            Active?.Play(Active.clearSfx, Vector3.zero, 0.7f, false, 0f);
        }

        public static void PlayFail()
        {
            Active?.Play(Active.failSfx, Vector3.zero, 0.75f, false, 0f);
        }

        public static void SetFireLoop(Transform host, bool playing)
        {
            Active?.SetLoop(host, FireLoopKey, Active.fireLoopSfx, playing, FireLoopVolume);
        }

        public static void SetSawLoop(Transform host, bool playing)
        {
            Active?.SetLoop(host, SawLoopKey, Active.sawLoopSfx, playing, SawLoopVolume);
        }

        private void Update()
        {
            if (loopCount <= 0)
            {
                return;
            }

            UpdateLoopVolumes();
        }

        private void SetLoop(Transform host, string key, AudioClip clip, bool playing, float volume)
        {
            if (host == null)
            {
                return;
            }

            string childName = "DungeonLoop_" + key;
            Transform child = host.Find(childName);
            if (!playing)
            {
                UnregisterLoop(host);
                if (child != null)
                {
                    AudioSource existing = child.GetComponent<AudioSource>();
                    if (existing != null)
                    {
                        existing.Stop();
                    }
                }

                return;
            }

            if (clip == null)
            {
                return;
            }

            AudioSource source;
            if (child == null)
            {
                GameObject loopObject = new GameObject(childName);
                loopObject.transform.SetParent(host, false);
                source = loopObject.AddComponent<AudioSource>();
                ConfigureWorldSource(source, true);
                source.outputAudioMixerGroup = sfxGroup;
            }
            else
            {
                source = child.GetComponent<AudioSource>();
                if (source == null)
                {
                    return;
                }

                ConfigureWorldSource(source, true);
            }

            source.clip = clip;
            RegisterLoop(host, source, volume);
            source.volume = MixSfx(volume);
            if (!source.isPlaying)
            {
                source.Play();
            }

            UpdateLoopVolumes();
        }

        private void Play(AudioClip clip, Vector3 position, float volume, bool worldSfx, float pitchJitter)
        {
            if (clip == null)
            {
                return;
            }

            if (worldSfx && !IsWithinHearRadius(position))
            {
                return;
            }

            float mixed = MixSfx(volume);
            float pitch = pitchJitter <= 0f ? 1f : Random.Range(1f - pitchJitter, 1f + pitchJitter);
            AudioSource source = worldSfx ? NextOneShotVoice() : uiSource;
            if (source == null)
            {
                return;
            }

            source.Stop();
            source.transform.position = worldSfx ? position : transform.position;
            source.clip = clip;
            source.volume = mixed;
            source.pitch = Mathf.Clamp(pitch, 0.01f, 3f);
            source.Play();
        }

        private void UpdateLoopVolumes()
        {
            for (int i = loopCount - 1; i >= 0; i--)
            {
                LoopVoice loop = loops[i];
                if (loop.Host == null || loop.Source == null)
                {
                    RemoveLoopAt(i);
                    continue;
                }

                bool audible = IsWithinHearRadius(loop.Host.position);
                loop.Source.volume = audible ? MixSfx(loop.Volume) : 0f;
                if (audible && loop.Clip != null && !loop.Source.isPlaying)
                {
                    loop.Source.clip = loop.Clip;
                    loop.Source.Play();
                }
            }
        }

        private void RegisterLoop(Transform host, AudioSource source, float volume)
        {
            for (int i = 0; i < loopCount; i++)
            {
                if (loops[i].Host == host)
                {
                    loops[i] = new LoopVoice(host, source, source.clip, volume);
                    return;
                }
            }

            if (loopCount >= loops.Length)
            {
                return;
            }

            loops[loopCount++] = new LoopVoice(host, source, source.clip, volume);
        }

        private void UnregisterLoop(Transform host)
        {
            for (int i = 0; i < loopCount; i++)
            {
                if (loops[i].Host == host)
                {
                    RemoveLoopAt(i);
                    return;
                }
            }
        }

        private void RemoveLoopAt(int index)
        {
            loopCount--;
            loops[index] = loops[loopCount];
            loops[loopCount] = default;
        }

        private void StopAllLoops()
        {
            for (int i = 0; i < loopCount; i++)
            {
                AudioSource source = loops[i].Source;
                if (source != null)
                {
                    source.Stop();
                }
            }

            loopCount = 0;
        }

        private bool IsWithinHearRadius(Vector3 position)
        {
            Vector3 delta = position - ResolveListenerPosition();
            delta.y = 0f;
            return delta.sqrMagnitude <= HearRadius * HearRadius;
        }

        private Vector3 ResolveListenerPosition()
        {
            if (listener == null)
            {
                Transform player = DemoDungeonController.Active != null ? DemoDungeonController.Active.PlayerTransform : null;
                listener = player != null
                    ? player
                    : Camera.main != null ? Camera.main.transform : transform;
            }

            return listener.position;
        }

        private void BuildOneShotVoices()
        {
            oneShotVoices = new AudioSource[OneShotVoiceCount];
            for (int i = 0; i < OneShotVoiceCount; i++)
            {
                oneShotVoices[i] = CreateSource($"SfxVoice {i + 1:00}", sfxGroup, false, 0f);
                ConfigureWorldSource(oneShotVoices[i], false);
            }
        }

        private AudioSource NextOneShotVoice()
        {
            if (oneShotVoices == null || oneShotVoices.Length == 0)
            {
                return null;
            }

            AudioSource source = oneShotVoices[oneShotIndex];
            oneShotIndex = (oneShotIndex + 1) % oneShotVoices.Length;
            return source;
        }

        private AudioSource CreateSource(string name, AudioMixerGroup group, bool loop, float spatialBlend)
        {
            GameObject sourceObject = new GameObject(name);
            sourceObject.transform.SetParent(transform, false);
            AudioSource source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = spatialBlend;
            source.dopplerLevel = 0f;
            source.outputAudioMixerGroup = group;
            return source;
        }

        private static void ConfigureWorldSource(AudioSource source, bool loop)
        {
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
        }

        private Bed CreateBed(string name, AudioMixerGroup group, AudioClip clip, float volume, bool useBgmScale)
        {
            AudioSource source = CreateSource(name, group, true, 0f);
            source.volume = Mix(volume, useBgmScale);
            return new Bed(source, clip, volume, useBgmScale);
        }

        private void PlayBed(Bed bed)
        {
            if (bed.Source == null || bed.Clip == null)
            {
                return;
            }

            bed.Source.clip = bed.Clip;
            bed.Source.volume = Mix(bed.Volume, bed.UseBgmScale);
            if (!bed.Source.isPlaying)
            {
                bed.Source.Play();
            }
        }

        private void ApplyBedVolumes()
        {
            if (!bedsPlaying || beds == null)
            {
                return;
            }

            for (int i = 0; i < beds.Length; i++)
            {
                Bed bed = beds[i];
                if (bed.Source != null)
                {
                    bed.Source.volume = Mix(bed.Volume, bed.UseBgmScale);
                }
            }
        }

        private float Mix(float volume, bool useBgmScale)
        {
            float channel = useBgmScale
                ? (bgmGroup == null ? AudioRuntimeSettings.BgmVolume : 1f)
                : SfxChannelScale();
            float master = useBgmScale ? MasterVolume : MasterVolume * SfxVolumeScale;
            return volume * master * channel;
        }

        private float MixSfx(float volume)
        {
            return volume * MasterVolume * SfxVolumeScale * SfxChannelScale();
        }

        private float SfxChannelScale()
        {
            return sfxGroup == null ? AudioRuntimeSettings.SfxVolume : 1f;
        }

        private readonly struct Bed
        {
            public Bed(AudioSource source, AudioClip clip, float volume, bool useBgmScale)
            {
                Source = source;
                Clip = clip;
                Volume = volume;
                UseBgmScale = useBgmScale;
            }

            public AudioSource Source { get; }
            public AudioClip Clip { get; }
            public float Volume { get; }
            public bool UseBgmScale { get; }
        }

        private readonly struct LoopVoice
        {
            public LoopVoice(Transform host, AudioSource source, AudioClip clip, float volume)
            {
                Host = host;
                Source = source;
                Clip = clip;
                Volume = volume;
            }

            public Transform Host { get; }
            public AudioSource Source { get; }
            public AudioClip Clip { get; }
            public float Volume { get; }
        }
    }
}
