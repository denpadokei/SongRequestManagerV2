using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using Zenject;

namespace SongRequestManagerV2.Models
{
    public class NotifySound : MonoBehaviour, IInitializable
    {
        public static readonly string SOUNDFOLDER = Path.Combine(Plugin.DataPath, "NotifySound");
        private AudioSource audioSource;
        private AudioClip notifySoundClip;

        public void Initialize()
        {
            Dispatcher.RunCoroutine(this.LoadSound());
        }

        public void PlaySound()
        {
            this.audioSource.volume = RequestBotConfig.Instance.SoundVolume / 100f;
            this.audioSource.PlayOneShot(this.notifySoundClip);
        }

        private IEnumerator LoadSound()
        {
            this.audioSource = this.gameObject.AddComponent<AudioSource>();
            if (!Directory.Exists(SOUNDFOLDER)) {
                Directory.CreateDirectory(SOUNDFOLDER);
            }
            var soundPath = Directory.EnumerateFiles(SOUNDFOLDER, "*.wav", SearchOption.TopDirectoryOnly).FirstOrDefault();
            Logger.Debug(soundPath);
            if (string.IsNullOrEmpty(soundPath)) {
                yield break;
            }
            var sound = UnityWebRequestMultimedia.GetAudioClip(soundPath, AudioType.WAV);
            yield return sound.SendWebRequest();
            if (!string.IsNullOrEmpty(sound.error)) {
                Logger.Error($"{sound.error}");
            }
            else {
                this.notifySoundClip = DownloadHandlerAudioClip.GetContent(sound);
                this.notifySoundClip.name = Path.GetFileName(soundPath);
                yield return new WaitWhile(() => !this.notifySoundClip);
            }
        }
    }
}
