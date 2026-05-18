#nullable enable
namespace UniT.Audio
{
    using System;
    using System.Threading;
    using Cysharp.Threading.Tasks;
    using UnityEngine;

    public interface IAudioManager : IDisposable
    {
        #region Sound

        public event Action EffectiveSoundVolumeChanged;

        public event Action EffectiveMuteSoundChanged;

        public float EffectiveSoundVolume { get; }

        public bool EffectiveMuteSound { get; }

        public void RegisterSoundSource(AudioSource source);

        public void UnregisterSoundSource(AudioSource source);

        public void LoadSound(AudioClip clip);

        public UniTask LoadSoundAsync(object key, IProgress<float>? progress = null, CancellationToken cancellationToken = default);

        public void PlaySoundOneShot(AudioClip clip);

        public void PlaySoundOneShot(object key);

        public void PlaySound(AudioClip clip, bool loop = false, bool force = false);

        public void PlaySound(object key, bool loop = false, bool force = false);

        public bool IsSoundPlaying(AudioClip clip);

        public bool IsSoundPlaying(object key);

        public float GetSoundTime(AudioClip clip);

        public float GetSoundTime(object key);

        public void SetSoundTime(AudioClip clip, float time);

        public void SetSoundTime(object key, float time);

        public void PauseSound(AudioClip clip);

        public void PauseSound(object key);

        public void PauseAllSounds();

        public void ResumeSound(AudioClip clip);

        public void ResumeSound(object key);

        public void ResumeAllSounds();

        public void StopSound(AudioClip clip);

        public void StopSound(object key);

        public void StopAllSounds();

        public void UnloadSound(AudioClip clip);

        public void UnloadSound(object key);

        public void UnloadAllSounds();

        #endregion

        #region Music

        public event Action EffectiveMusicVolumeChanged;

        public event Action EffectiveMuteMusicChanged;

        public float EffectiveMusicVolume { get; }

        public bool EffectiveMuteMusic { get; }

        public void LoadMusic(AudioClip clip);

        public UniTask LoadMusicAsync(object key, IProgress<float>? progress = null, CancellationToken cancellationToken = default);

        public void PlayMusic(AudioClip clip, bool loop = true, bool force = false);

        public void PlayMusic(object key, bool loop = true, bool force = false);

        public bool IsMusicPlaying();

        public float GetMusicTime();

        public void SetMusicTime(float time);

        public void PauseMusic();

        public void ResumeMusic();

        public void StopMusic();

        public void UnloadMusic(AudioClip clip);

        public void UnloadMusic(object key);

        public void UnloadAllMusics();

        #endregion
    }
}