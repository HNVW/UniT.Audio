#nullable enable
namespace UniT.Audio.Default
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Threading;
    using Cysharp.Threading.Tasks;
    using UniT.Extensions;
    using UniT.Logging;
    using UniT.ResourceManagement;
    using UnityEngine;
    using ILogger = Logging.ILogger;

    public sealed class AudioPool : IAudioPool, IDisposable
    {
        private readonly AudioSettings masterSettings;
        private readonly IAssetManager assetManager;
        private readonly GameObject sourcesContainer;
        private readonly Stack<AudioSource> sourcePool;
        private readonly ILogger logger;

        private readonly AudioSettings settings = new();
        private readonly HashSet<AudioSource> registeredSources = new();
        private readonly Dictionary<object, AudioClip> keyToClip = new();
        private readonly Dictionary<AudioClip, AudioSource> clipToSource = new();
        private readonly Dictionary<AudioSource, float> sourceToVolumeScale = new();

        public AudioPool(AudioSettings masterSettings, IAssetManager assetManager, GameObject sourcesContainer, Stack<AudioSource> sourcePool, ILogger logger)
        {
            this.masterSettings = masterSettings;
            this.assetManager = assetManager;
            this.sourcesContainer = sourcesContainer;
            this.sourcePool = sourcePool;
            this.logger = logger;

            this.masterSettings.VolumeChanged += this.OnVolumeChanged;
            this.masterSettings.MuteChanged += this.OnMuteChanged;
            this.settings.VolumeChanged += this.OnVolumeChanged;
            this.settings.MuteChanged += this.OnMuteChanged;
        }

        #region Public

        IAudioSettings IAudioPool.Settings => this.settings;

        event Action IAudioPool.VolumeChanged { add => this.volumeChanged += value; remove => this.volumeChanged -= value; }

        event Action IAudioPool.MuteChanged { add => this.muteChanged += value; remove => this.muteChanged -= value; }

        float IAudioPool.Volume => this.masterSettings.Volume * this.settings.Volume;

        bool IAudioPool.Mute => this.masterSettings.Mute || this.settings.Mute;

        void IAudioPool.RegisterSource(AudioSource source)
        {
            this.Configure(source);
            this.registeredSources.Add(source);
        }

        void IAudioPool.UnregisterSource(AudioSource source)
        {
            this.registeredSources.Remove(source);
        }

        void IAudioPool.Load(AudioClip clip) => this.Load(clip);

        async UniTask IAudioPool.LoadAsync(object key, IProgress<float>? progress, CancellationToken cancellationToken)
        {
            var clip = await this.keyToClip.GetOrAddAsync(key, static state => state.assetManager.LoadAsync<AudioClip>(state.key, state.progress, state.cancellationToken), (this.assetManager, key, progress, cancellationToken));
            this.Load(clip);
        }

        void IAudioPool.PlayOneShot(AudioClip clip) => this.PlayOneShot(clip);

        void IAudioPool.PlayOneShot(object key)
        {
            var clip = this.GetClipOrThrow(key);
            this.PlayOneShot(clip);
        }

        void IAudioPool.Play(AudioClip clip, bool loop, bool force) => this.Play(clip, loop, force);

        void IAudioPool.Play(object key, bool loop, bool force)
        {
            var clip = this.GetClipOrThrow(key);
            this.Play(clip, loop, force);
        }

        void IAudioPool.SetVolumeScale(AudioClip clip, float value) => this.SetVolumeScale(clip, value);

        void IAudioPool.SetVolumeScale(object key, float value)
        {
            var clip = this.GetClipOrThrow(key);
            this.SetVolumeScale(clip, value);
        }

        bool IAudioPool.IsPlaying(AudioClip clip) => this.IsPlaying(clip);

        bool IAudioPool.IsPlaying(object key)
        {
            if (!this.TryGetClip(key, out var clip)) return false;
            return this.IsPlaying(clip);
        }

        float IAudioPool.GetTime(AudioClip clip) => this.GetTime(clip);

        float IAudioPool.GetTime(object key)
        {
            var clip = this.GetClipOrThrow(key);
            return this.GetTime(clip);
        }

        void IAudioPool.SetTime(AudioClip clip, float time) => this.SetTime(clip, time);

        void IAudioPool.SetTime(object key, float time)
        {
            var clip = this.GetClipOrThrow(key);
            this.SetTime(clip, time);
        }

        void IAudioPool.Pause(AudioClip clip) => this.Pause(clip);

        void IAudioPool.Pause(object key)
        {
            var clip = this.GetClipOrThrow(key);
            this.Pause(clip);
        }

        void IAudioPool.PauseAll()
        {
            this.clipToSource.Keys.ForEach(this.Pause);
        }

        void IAudioPool.Resume(AudioClip clip) => this.Resume(clip);

        void IAudioPool.Resume(object key)
        {
            var clip = this.GetClipOrThrow(key);
            this.Resume(clip);
        }

        void IAudioPool.ResumeAll()
        {
            this.clipToSource.Keys.ForEach(this.Resume);
        }

        void IAudioPool.Stop(AudioClip clip) => this.Stop(clip);

        void IAudioPool.Stop(object key)
        {
            if (!this.TryGetClip(key, out var clip)) return;
            this.Stop(clip);
        }

        void IAudioPool.StopAll()
        {
            this.clipToSource.Keys.ForEach(this.Stop);
        }

        void IAudioPool.Unload(AudioClip clip) => this.Unload(clip);

        void IAudioPool.Unload(object key) => this.Unload(key);

        void IAudioPool.UnloadAll()
        {
            this.keyToClip.Keys.SafeForEach(this.Unload);
            this.clipToSource.Keys.SafeForEach(this.Unload);
        }

        #endregion

        #region Private

        private Action? volumeChanged;
        private Action? muteChanged;

        private void Load(AudioClip clip)
        {
            this.clipToSource.TryAdd(clip, static state =>
            {
                var source = state.@this.sourcePool.PopOrDefault(static sourcesContainer => sourcesContainer.AddComponent<AudioSource>(), state.@this.sourcesContainer);
                source.clip = state.clip;
                state.@this.Configure(source);
                state.@this.logger.Debug($"Loaded {state.clip.name}");
                return source;
            }, (@this: this, clip));
        }

        private void PlayOneShot(AudioClip clip)
        {
            var source = this.GetSourceOrLoad(clip);
            source.PlayOneShot(source.clip);
            this.logger.Debug($"Playing one shot {clip.name}");
        }

        private void Play(AudioClip clip, bool loop, bool force)
        {
            var source = this.GetSourceOrLoad(clip);
            source.loop = loop;
            if (!force && source.isPlaying) return;
            source.Play();
            this.logger.Debug($"Playing {clip.name}, loop: {loop}");
        }

        private void SetVolumeScale(AudioClip clip, float value)
        {
            var source = this.GetSourceOrLoad(clip);
            this.sourceToVolumeScale[source] = value;
            this.ConfigureVolume(source);
            this.logger.Debug($"Set {clip.name} volumeScale to {value}");
        }

        private bool IsPlaying(AudioClip clip)
        {
            if (!this.TryGetSource(clip, out var source)) return false;
            return source.isPlaying;
        }

        private float GetTime(AudioClip clip)
        {
            var source = this.GetSourceOrLoad(clip);
            return source.time;
        }

        private void SetTime(AudioClip clip, float time)
        {
            var source = this.GetSourceOrLoad(clip);
            source.time = time;
            this.logger.Debug($"Set {clip.name} time to {time}");
        }

        private void Pause(AudioClip clip)
        {
            var source = this.GetSourceOrLoad(clip);
            source.Pause();
            this.logger.Debug($"Paused {clip.name}");
        }

        private void Resume(AudioClip clip)
        {
            var source = this.GetSourceOrLoad(clip);
            source.UnPause();
            this.logger.Debug($"Resumed {clip.name}");
        }

        private void Stop(AudioClip clip)
        {
            if (!this.TryGetSource(clip, out var source)) return;
            source.Stop();
            this.logger.Debug($"Stopped {clip.name}");
        }

        private void Unload(AudioClip clip)
        {
            if (!this.TryGetSource(clip, out var source)) return;
            this.sourceToVolumeScale.Remove(source);
            if (source)
            {
                source.Stop();
                source.clip = null;
                this.sourcePool.Push(source);
            }
            this.clipToSource.Remove(clip);
            this.logger.Debug($"Unloaded {clip.name}");
        }

        private void Unload(object key)
        {
            if (!this.TryGetClip(key, out var clip)) return;
            this.Unload(clip);
            this.assetManager.Unload(key);
            this.keyToClip.Remove(key);
        }

        private AudioSource GetSourceOrLoad(AudioClip clip)
        {
            if (this.clipToSource.TryGetValue(clip, out var source)) return source;
            this.Load(clip);
            this.logger.Warning($"Auto loaded {clip.name}. Consider preload it with `Load` for better performance.");
            return this.clipToSource[clip];
        }

        private AudioClip GetClipOrThrow(object key)
        {
            return this.keyToClip.TryGetValue(key, out var clip)
                ? clip
                : throw new InvalidOperationException($"{key} not loaded. Load it with `LoadAsync`.");
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

        private void OnVolumeChanged()
        {
            this.clipToSource.ForEach(this.ConfigureVolume);
            this.registeredSources.ForEach(this.ConfigureVolume);
            this.volumeChanged?.Invoke();
        }

        private void OnMuteChanged()
        {
            this.clipToSource.ForEach(this.ConfigureMute);
            this.registeredSources.ForEach(this.ConfigureMute);
            this.muteChanged?.Invoke();
        }

        private void Configure(AudioSource source)
        {
            this.ConfigureVolume(source);
            this.ConfigureMute(source);
        }

        private void ConfigureVolume(AudioSource source)
        {
            source.volume = this.masterSettings.Volume * this.settings.Volume * this.sourceToVolumeScale.GetValueOrDefault(source, 1);
        }

        private void ConfigureMute(AudioSource source)
        {
            source.mute = this.masterSettings.Mute || this.settings.Mute;
        }

        #endregion

        void IDisposable.Dispose()
        {
            this.registeredSources.Clear();
            this.keyToClip.Keys.SafeForEach(this.Unload);
            this.clipToSource.Keys.SafeForEach(this.Unload);

            this.masterSettings.VolumeChanged -= this.OnVolumeChanged;
            this.masterSettings.MuteChanged -= this.OnMuteChanged;
            this.settings.VolumeChanged -= this.OnVolumeChanged;
            this.settings.MuteChanged -= this.OnMuteChanged;
        }
    }
}