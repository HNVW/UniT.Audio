#nullable enable
namespace UniT.Audio
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Threading;
    using Cysharp.Threading.Tasks;
    using UniT.Extensions;
    using UniT.Extensions.UniTask;
    using UniT.Logging;
    using UniT.ResourceManagement;
    using UnityEngine;
    using ILogger = UniT.Logging.ILogger;

    public sealed class AudioPool : IDisposable
    {
        private readonly AudioSettings      masterSettings;
        private readonly IAssetsManager     assetsManager;
        private readonly GameObject         sourcesContainer;
        private readonly Stack<AudioSource> sourcePool;
        private readonly ILogger            logger;

        private readonly HashSet<AudioSource>               registeredSources = new();
        private readonly Dictionary<object, AudioClip>      keyToClip         = new();
        private readonly Dictionary<AudioClip, AudioSource> clipToSource      = new();

        public AudioPool(AudioSettings masterSettings, IAssetsManager assetsManager, GameObject sourcesContainer, Stack<AudioSource> sourcePool, ILogger logger)
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
                var source = state.@this.sourcePool.PopOrDefault(static sourcesContainer => sourcesContainer.AddComponent<AudioSource>(), state.@this.sourcesContainer);
                state.@this.Configure(source);
                source.clip = state.clip;
                state.@this.logger.Debug($"Loaded {state.clip.name}");
                return source;
            }, (@this: this, clip));
        }

        public async UniTask LoadAsync(object key, IProgress<float>? progress, CancellationToken cancellationToken)
        {
            var clip = await this.keyToClip.GetOrAddAsync(key, static state => state.assetsManager.LoadAsync<AudioClip>(state.key, state.progress, state.cancellationToken), (this.assetsManager, key, progress, cancellationToken));
            this.Load(clip);
        }

        public void PlayOneShot(AudioClip clip)
        {
            var source = this.GetOrLoadSource(clip);
            source.PlayOneShot(source.clip);
            this.logger.Debug($"Playing one shot {clip.name}");
        }

        public void PlayOneShot(object key)
        {
            if (!this.keyToClip.TryGetValue(key, out var clip)) throw new InvalidOperationException($"{key} not loaded. Load it with `LoadAsync`.");
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
            if (!this.keyToClip.TryGetValue(key, out var clip)) throw new InvalidOperationException($"{key} not loaded. Load it with `LoadAsync`.");
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
            if (source)
            {
                source.Stop();
                source.clip = null;
                this.sourcePool.Push(source);
            }
            this.clipToSource.Remove(clip);
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
            this.logger.Warning($"Auto loaded {clip.name}. Consider preload it with `Load` for better performance.");
            return source;
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

        public void Dispose()
        {
            this.registeredSources.Clear();
            this.keyToClip.Keys.SafeForEach(this.Unload);
            this.clipToSource.Keys.SafeForEach(this.Unload);
        }
    }
}