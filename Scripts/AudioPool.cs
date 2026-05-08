#nullable enable
namespace UniT.Audio
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using UniT.Extensions;
    using UniT.Logging;
    using UniT.ResourceManagement;
    using UnityEngine;
    using ILogger = UniT.Logging.ILogger;
    #if UNIT_UNITASK
    using System.Threading;
    using Cysharp.Threading.Tasks;
    #else
    using System.Collections;
    #endif

    public sealed class AudioPool
    {
        private readonly AudioSettings      masterSettings;
        private readonly IAssetsManager     assetsManager;
        private readonly GameObject         sourcesContainer;
        private readonly Queue<AudioSource> sourcePool;
        private readonly ILogger            logger;

        private readonly HashSet<AudioSource>               registeredSources = new();
        private readonly Dictionary<object, AudioClip>      keyToClip         = new();
        private readonly Dictionary<AudioClip, AudioSource> clipToSource      = new();

        public AudioPool(AudioSettings masterSettings, IAssetsManager assetsManager, GameObject sourcesContainer, Queue<AudioSource> sourcePool, ILogger logger)
        {
            this.masterSettings   = masterSettings;
            this.assetsManager    = assetsManager;
            this.sourcesContainer = sourcesContainer;
            this.sourcePool       = sourcePool;
            this.logger           = logger;

            masterSettings.VolumeChanged += this.OnEffectiveVolumeChanged;
            masterSettings.MuteChanged   += this.OnEffectiveMuteChanged;

            this.Settings.VolumeChanged += this.OnEffectiveVolumeChanged;
            this.Settings.MuteChanged   += this.OnEffectiveMuteChanged;
        }

        public AudioSettings Settings { get; } = new();

        #region Public

        public event Action? EffectiveVolumeChanged;

        public event Action? EffectiveMuteChanged;

        public float EffectiveVolume => this.Settings.Volume * this.masterSettings.Volume;

        public bool EffectiveMute => this.Settings.Mute || this.masterSettings.Mute;

        public void Register(AudioSource source)
        {
            this.Configure(source);
            this.registeredSources.Add(source);
        }

        public void Unregister(AudioSource source)
        {
            this.registeredSources.Remove(source);
        }

        public AudioSource Load(AudioClip clip)
        {
            return this.clipToSource.GetOrAdd(clip, static state =>
            {
                var source = state.@this.sourcePool.DequeueOrDefault(static sourcesContainer => sourcesContainer.AddComponent<AudioSource>(), state.@this.sourcesContainer);
                state.@this.Configure(source);
                source.clip = state.clip;
                state.@this.logger.Debug($"Loaded {state.clip.name}");
                return source;
            }, (@this: this, clip));
        }

        #if !UNITY_WEBGL
        public void Load(object key)
        {
            var clip = this.keyToClip.GetOrAdd(key, static state => state.assetsManager.Load<AudioClip>(state.key), (this.assetsManager, key));
            this.Load(clip);
        }
        #endif

        #if UNIT_UNITASK
        public async UniTask LoadAsync(object key, IProgress<float>? progress, CancellationToken cancellationToken)
        {
            var clip = await this.keyToClip.GetOrAddAsync(key, static state => state.assetsManager.LoadAsync<AudioClip>(state.key, state.progress, state.cancellationToken), (this.assetsManager, key, progress, cancellationToken));
            this.Load(clip);
        }
        #else
            public IEnumerator LoadAsync(object key, Action? callback, IProgress<float>? progress)
            {
                var clip = default(AudioClip)!;
                yield return this.keyToClip.GetOrAddAsync(
                    key,
                    callback => this.assetsManager.LoadAsync(key, callback, progress),
                    result => clip = result
                );
                this.Load(clip);
                callback?.Invoke();
            }
        #endif

        public void PlayOneShot(AudioClip clip)
        {
            var source = this.GetOrLoadSource(clip);
            source.PlayOneShot(source.clip);
            this.logger.Debug($"Playing one shot {clip.name}");
        }

        public void PlayOneShot(object key)
        {
            var clip = this.GetOrLoadClip(key);
            this.PlayOneShot(clip);
        }

        public void Play(AudioClip clip, bool loop, bool force)
        {
            var source = this.GetOrLoadSource(clip);
            source.loop = loop;
            if (!force && source.isPlaying) return;
            source.Play();
            this.logger.Debug($"Playing {clip.name}, loop: {loop}");
        }

        public void Play(object key, bool loop, bool force)
        {
            var clip = this.GetOrLoadClip(key);
            this.Play(clip, loop, force);
        }

        public bool IsPlaying(AudioClip clip)
        {
            if (!this.TryGetSource(clip, out var source)) return false;
            return source.isPlaying;
        }

        public bool IsPlaying(object key)
        {
            if (!this.TryGetClip(key, out var clip)) return false;
            return this.IsPlaying(clip);
        }

        public float GetTime(AudioClip clip)
        {
            if (!this.TryGetSource(clip, out var source)) return 0;
            return source.time;
        }

        public float GetTime(object key)
        {
            if (!this.TryGetClip(key, out var clip)) return 0;
            return this.GetTime(clip);
        }

        public void SetTime(AudioClip clip, float time)
        {
            if (!this.TryGetSource(clip, out var source)) return;
            source.time = time;
            this.logger.Debug($"Set {clip.name} time to {time}");
        }

        public void SetTime(object key, float time)
        {
            if (!this.TryGetClip(key, out var clip)) return;
            this.SetTime(clip, time);
        }

        public void Pause(AudioClip clip)
        {
            if (!this.TryGetSource(clip, out var source)) return;
            source.Pause();
            this.logger.Debug($"Paused {clip.name}");
        }

        public void Pause(object key)
        {
            if (!this.TryGetClip(key, out var clip)) return;
            this.Pause(clip);
        }

        public void PauseAll()
        {
            this.clipToSource.Keys.ForEach(this.Pause);
        }

        public void Resume(AudioClip clip)
        {
            if (!this.TryGetSource(clip, out var source)) return;
            source.UnPause();
            this.logger.Debug($"Resumed {clip.name}");
        }

        public void Resume(object key)
        {
            if (!this.TryGetClip(key, out var clip)) return;
            this.Resume(clip);
        }

        public void ResumeAll()
        {
            this.clipToSource.Keys.ForEach(this.Resume);
        }

        public void Stop(AudioClip clip)
        {
            if (!this.TryGetSource(clip, out var source)) return;
            source.Stop();
            this.logger.Debug($"Stopped {clip.name}");
        }

        public void Stop(object key)
        {
            if (!this.TryGetClip(key, out var clip)) return;
            this.Stop(clip);
        }

        public void StopAll()
        {
            this.clipToSource.Keys.ForEach(this.Stop);
        }

        public void Unload(AudioClip clip)
        {
            if (!this.TryGetSource(clip, out var source)) return;
            source.Stop();
            source.clip = null;
            this.clipToSource.Remove(clip);
            this.sourcePool.Enqueue(source);
            this.logger.Debug($"Unloaded {clip.name}");
        }

        public void Unload(object key)
        {
            if (!this.TryGetClip(key, out var clip)) return;
            this.Unload(clip);
            this.assetsManager.Unload(key);
            this.keyToClip.Remove(key);
        }

        public void UnloadAll()
        {
            this.keyToClip.Keys.SafeForEach(this.Unload);
            this.clipToSource.Keys.SafeForEach(this.Unload);
        }

        #endregion

        #region Private

        private void OnEffectiveVolumeChanged()
        {
            this.clipToSource.ForEach(this.ConfigureVolume);
            this.registeredSources.ForEach(this.ConfigureVolume);
            this.EffectiveVolumeChanged?.Invoke();
        }

        private void OnEffectiveMuteChanged()
        {
            this.clipToSource.ForEach(this.ConfigureMute);
            this.registeredSources.ForEach(this.ConfigureMute);
            this.EffectiveMuteChanged?.Invoke();
        }

        private void Configure(AudioSource source)
        {
            this.ConfigureVolume(source);
            this.ConfigureMute(source);
        }

        private void ConfigureVolume(AudioSource source)
        {
            source.volume = this.EffectiveVolume;
        }

        private void ConfigureMute(AudioSource source)
        {
            source.mute = this.EffectiveMute;
        }

        private AudioSource GetOrLoadSource(AudioClip clip)
        {
            if (this.clipToSource.TryGetValue(clip, out var source)) return source;
            source = this.Load(clip);
            this.logger.Warning($"Auto loaded {clip.name}. Consider preload it with `Load` or `LoadAsync` for better performance.");
            return source;
        }

        private AudioClip GetOrLoadClip(object key)
        {
            return this.keyToClip.GetOrAdd(key, static state =>
            {
                #if !UNITY_WEBGL
                return state.assetsManager.Load<AudioClip>(state.key);
                #else
                    throw new NotSupportedException("Cannot directly Play with key on WebGL. Please preload it with `LoadAsync`.");
                #endif
            }, (this.assetsManager, key));
        }

        private bool TryGetSource(AudioClip clip, [MaybeNullWhen(false)] out AudioSource source)
        {
            if (this.clipToSource.TryGetValue(clip, out source)) return true;
            this.logger.Warning($"{clip.name} not loaded");
            return false;
        }

        private bool TryGetClip(object key, [MaybeNullWhen(false)] out AudioClip clip)
        {
            if (this.keyToClip.TryGetValue(key, out clip)) return true;
            this.logger.Warning($"{key} not loaded");
            return false;
        }

        #endregion
    }
}