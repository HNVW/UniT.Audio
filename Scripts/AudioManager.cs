#nullable enable
namespace UniT.Audio
{
    using System;
    using System.Collections.Generic;
    using UniT.Extensions;
    using UniT.Logging;
    using UniT.ResourceManagement;
    using UnityEngine;
    using UnityEngine.Scripting;
    using ILogger = UniT.Logging.ILogger;
    using Object = UnityEngine.Object;
    #if UNIT_UNITASK
    using System.Threading;
    using Cysharp.Threading.Tasks;
    #else
    using System.Collections;
    #endif

    public sealed class AudioManager : IAudioManagerSettings, IAudioManager
    {
        #region Constructor

        private readonly ILogger logger;

        private readonly GameObject         sourceContainer = new GameObject(nameof(AudioManager)).DontDestroyOnLoad();
        private readonly Stack<AudioSource> sourcePool      = new();
        private readonly AudioSettings      masterSettings  = new();
        private readonly AudioPool          soundPool;
        private readonly AudioPool          musicPool;

        [Preserve]
        public AudioManager(IAssetsManager assetsManager, ILoggerManager loggerManager)
        {
            this.logger = loggerManager.GetLogger(this);

            this.soundPool = new(this.masterSettings, assetsManager, this.sourceContainer, this.sourcePool, this.logger);
            this.musicPool = new(this.masterSettings, assetsManager, this.sourceContainer, this.sourcePool, this.logger);

            this.logger.Debug("Constructed");
        }

        #endregion

        #region Settings

        AudioSettings IAudioManagerSettings.MasterSettings => this.masterSettings;
        AudioSettings IAudioManagerSettings.SoundSettings  => this.soundPool.Settings;
        AudioSettings IAudioManagerSettings.MusicSettings  => this.musicPool.Settings;

        #endregion

        #region Sound

        event Action IAudioManager.EffectiveSoundVolumeChanged { add => this.soundPool.EffectiveVolumeChanged += value; remove => this.soundPool.EffectiveVolumeChanged -= value; }

        event Action IAudioManager.EffectiveMuteSoundChanged { add => this.soundPool.EffectiveMuteChanged += value; remove => this.soundPool.EffectiveMuteChanged -= value; }

        float IAudioManager.EffectiveSoundVolume => this.soundPool.EffectiveVolume;

        bool IAudioManager.EffectiveMuteSound => this.soundPool.EffectiveMute;

        void IAudioManager.RegisterSoundSource(AudioSource source) => this.soundPool.Register(source);

        void IAudioManager.UnregisterSoundSource(AudioSource soundSource) => this.soundPool.Unregister(soundSource);

        void IAudioManager.LoadSound(AudioClip clip) => this.soundPool.Load(clip);

        #if !UNITY_WEBGL
        void IAudioManager.LoadSound(object key) => this.soundPool.Load(key);
        #endif

        #if UNIT_UNITASK
        UniTask IAudioManager.LoadSoundAsync(object key, IProgress<float>? progress, CancellationToken cancellationToken) => this.soundPool.LoadAsync(key, progress, cancellationToken);
        #else
        IEnumerator IAudioManager.LoadSoundAsync(object key, Action? callback, IProgress<float>? progress) => this.soundPool.LoadAsync(key, callback, progress);
        #endif

        void IAudioManager.PlaySoundOneShot(AudioClip clip) => this.soundPool.PlayOneShot(clip);

        void IAudioManager.PlaySoundOneShot(object key) => this.soundPool.PlayOneShot(key);

        void IAudioManager.PlaySound(AudioClip clip, bool loop, bool force) => this.soundPool.Play(clip, loop, force);

        void IAudioManager.PlaySound(object key, bool loop, bool force) => this.soundPool.Play(key, loop, force);

        bool IAudioManager.IsSoundPlaying(AudioClip clip) => this.soundPool.IsPlaying(clip);

        bool IAudioManager.IsSoundPlaying(object key) => this.soundPool.IsPlaying(key);

        float IAudioManager.GetSoundTime(AudioClip clip) => this.soundPool.GetTime(clip);

        float IAudioManager.GetSoundTime(object key) => this.soundPool.GetTime(key);

        void IAudioManager.SetSoundTime(AudioClip clip, float time) => this.soundPool.SetTime(clip, time);

        void IAudioManager.SetSoundTime(object key, float time) => this.soundPool.SetTime(key, time);

        void IAudioManager.PauseSound(AudioClip clip) => this.soundPool.Pause(clip);

        void IAudioManager.PauseSound(object key) => this.soundPool.Pause(key);

        void IAudioManager.PauseAllSounds() => this.soundPool.PauseAll();

        void IAudioManager.ResumeSound(AudioClip clip) => this.soundPool.Resume(clip);

        void IAudioManager.ResumeSound(object key) => this.soundPool.Resume(key);

        void IAudioManager.ResumeAllSounds() => this.soundPool.ResumeAll();

        void IAudioManager.StopSound(AudioClip clip) => this.soundPool.Stop(clip);

        void IAudioManager.StopSound(object key) => this.soundPool.Stop(key);

        void IAudioManager.StopAllSounds() => this.soundPool.StopAll();

        void IAudioManager.UnloadSound(AudioClip clip) => this.soundPool.Unload(clip);

        void IAudioManager.UnloadSound(object key) => this.soundPool.Unload(key);

        void IAudioManager.UnloadAllSounds() => this.soundPool.UnloadAll();

        #endregion

        #region Music

        private object? playingMusic;

        event Action IAudioManager.EffectiveMusicVolumeChanged { add => this.musicPool.EffectiveVolumeChanged += value; remove => this.musicPool.EffectiveVolumeChanged -= value; }

        event Action IAudioManager.EffectiveMuteMusicChanged { add => this.musicPool.EffectiveMuteChanged += value; remove => this.musicPool.EffectiveMuteChanged -= value; }

        float IAudioManager.EffectiveMusicVolume => this.musicPool.EffectiveVolume;

        bool IAudioManager.EffectiveMuteMusic => this.musicPool.EffectiveMute;

        void IAudioManager.LoadMusic(AudioClip clip) => this.musicPool.Load(clip);

        #if !UNITY_WEBGL
        void IAudioManager.LoadMusic(object key) => this.musicPool.Load(key);
        #endif

        #if UNIT_UNITASK
        UniTask IAudioManager.LoadMusicAsync(object key, IProgress<float>? progress, CancellationToken cancellationToken) => this.musicPool.LoadAsync(key, progress, cancellationToken);
        #else
        IEnumerator IAudioManager.LoadMusicAsync(object key, Action? callback, IProgress<float>? progress) => this.musicPool.LoadAsync(key, callback, progress);
        #endif

        void IAudioManager.PlayMusic(AudioClip clip, bool loop, bool force)
        {
            if (this.playingMusic is { } && !ReferenceEquals(this.playingMusic, clip))
            {
                this.musicPool.Stop(clip);
                this.playingMusic = null;
            }
            this.musicPool.Play(clip, loop, force);
            this.playingMusic = clip;
        }

        void IAudioManager.PlayMusic(object key, bool loop, bool force)
        {
            if (this.playingMusic is { } && this.playingMusic != key)
            {
                this.musicPool.Stop(key);
                this.playingMusic = null;
            }
            this.musicPool.Play(key, loop, force);
            this.playingMusic = key;
        }

        bool IAudioManager.IsMusicPlaying()
        {
            switch (this.playingMusic)
            {
                case AudioClip clip: return this.musicPool.IsPlaying(clip);
                case { } key:        return this.musicPool.IsPlaying(key);
                default:             return false;
            }
        }

        float IAudioManager.GetMusicTime()
        {
            switch (this.playingMusic)
            {
                case AudioClip clip: return this.musicPool.GetTime(clip);
                case { } key:        return this.musicPool.GetTime(key);
                default:             return 0;
            }
        }

        void IAudioManager.SetMusicTime(float time)
        {
            switch (this.playingMusic)
            {
                case AudioClip clip: this.musicPool.SetTime(clip, time); break;
                case { } key:        this.musicPool.SetTime(key, time); break;
            }
        }

        void IAudioManager.PauseMusic()
        {
            switch (this.playingMusic)
            {
                case AudioClip clip: this.musicPool.Pause(clip); break;
                case { } key:        this.musicPool.Pause(key); break;
            }
        }

        void IAudioManager.ResumeMusic()
        {
            switch (this.playingMusic)
            {
                case AudioClip clip: this.musicPool.Resume(clip); break;
                case { } key:        this.musicPool.Resume(key); break;
            }
        }

        void IAudioManager.StopMusic()
        {
            switch (this.playingMusic)
            {
                case AudioClip clip: this.musicPool.Stop(clip); break;
                case { } key:        this.musicPool.Stop(key); break;
            }
            this.playingMusic = null;
        }

        void IAudioManager.UnloadMusic(AudioClip clip)
        {
            this.musicPool.Unload(clip);
            if (ReferenceEquals(this.playingMusic, clip))
            {
                this.playingMusic = null;
            }
        }

        void IAudioManager.UnloadMusic(object key)
        {
            this.musicPool.Unload(key);
            if (this.playingMusic == key)
            {
                this.playingMusic = null;
            }
        }

        void IAudioManager.UnloadAllMusics()
        {
            this.musicPool.UnloadAll();
            this.playingMusic = null;
        }

        #endregion

        #region Finalizer

        private void Dispose()
        {
            this.soundPool.Dispose();
            this.musicPool.Dispose();
            this.sourcePool.Clear();
            if (this.sourceContainer) Object.Destroy(this.sourceContainer);
        }

        void IDisposable.Dispose()
        {
            this.Dispose();
            this.logger.Debug("Disposed");
        }

        ~AudioManager()
        {
            this.Dispose();
            this.logger.Debug("Finalized");
        }

        #endregion
    }
}