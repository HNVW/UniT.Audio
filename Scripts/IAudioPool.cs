#nullable enable
namespace UniT.Audio
{
    using System;
    using System.Threading;
    using Cysharp.Threading.Tasks;
    using UnityEngine;

    public interface IAudioPool
    {
        public IAudioSettings Settings { get; }

        public event Action VolumeChanged;

        public event Action MuteChanged;

        public float Volume { get; }

        public bool Mute { get; }

        public void RegisterSource(AudioSource source);

        public void UnregisterSource(AudioSource source);

        public void Load(AudioClip clip);

        public UniTask LoadAsync(object key, IProgress<float>? progress = null, CancellationToken cancellationToken = default);

        public void PlayOneShot(AudioClip clip);

        public void PlayOneShot(object key);

        public void Play(AudioClip clip, bool loop = false, bool force = false);

        public void Play(object key, bool loop = false, bool force = false);

        public void SetVolumeScale(AudioClip clip, float value);

        public void SetVolumeScale(object key, float value);

        public bool IsPlaying(AudioClip clip);

        public bool IsPlaying(object key);

        public float GetTime(AudioClip clip);

        public float GetTime(object key);

        public void SetTime(AudioClip clip, float time);

        public void SetTime(object key, float time);

        public void Pause(AudioClip clip);

        public void Pause(object key);

        public void PauseAll();

        public void Resume(AudioClip clip);

        public void Resume(object key);

        public void ResumeAll();

        public void Stop(AudioClip clip);

        public void Stop(object key);

        public void StopAll();

        public void Unload(AudioClip clip);

        public void Unload(object key);

        public void UnloadAll();
    }
}