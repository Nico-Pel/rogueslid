using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace YsoCorp {

    namespace GameUtils {

        [DefaultExecutionOrder(-15)]
        public sealed class SoundResourcesManager : MonoBehaviour {

            internal Dictionary<string, AudioClip> effects;
            internal Dictionary<string, AudioClip> musics;

            private void Awake() {
                this.effects = AResourcesManager.LoadDictionary<AudioClip>("Sounds/Effects");
                this.musics = AResourcesManager.LoadDictionary<AudioClip>("Sounds/Musics");
            }

        }

        [DefaultExecutionOrder(-12)]
        public sealed class SoundManager : BaseManager {

            private class SoundEffectSetup {
                public float delta = 0.03f;
                public string key = "";
                public float volume = 1f;
                public float pitch = 1f;
                public bool loop = false;
                public Transform spacialParent = null;
                public float spacialMaxDistance = 10f;
            }

            [SerializeField] private SoundElement pfSoundElement;

            private AudioSource _audioSourceMusics;
            private Dictionary<string, SoundElement> _effects = new Dictionary<string, SoundElement>();
            private SoundResourcesManager _rc;

            private float _targetMusicVolume;

            private T AddGameObject<T>(string n = "") where T : Component {
                if (n == null || n == "") {
                    n = typeof(T).Name;
                }
                GameObject g = new GameObject(n);
                g.transform.SetParent(this.transform);
                return g.AddComponent<T>();
            }

            private void Awake() {
                this.ycManager.soundManager = this;
                this._rc = this.AddGameObject<SoundResourcesManager>();
            }

            public AudioClip GetEffectAudioClip(string soundName) {
                return this._rc.effects[soundName];
            }

            public AudioClip GetMusicAudioClip(string soundName) {
                return this._rc.musics[soundName];
            }

            // EFFECT
            private bool CanEffects() {
                return this.ycManager.ycConfig.SoundEffect && this.AreEffectsEnabled() && this.GetEffectsVolume() > 0;
            }

            private AudioSource _PlayEffect(string soundName, SoundEffectSetup setup) {
                this.ClearDestroyedEffectObjects();
                if (this.CanEffects()) {
                    if (this._rc.effects.ContainsKey(soundName)) {
                        string key = soundName + setup.key;
                        if (this._effects.ContainsKey(key) == false || Time.time >= this._effects[key].time) {
                            SoundElement se = null;
                            AudioClip clip = this._rc.effects[soundName];
                            if (this._effects.ContainsKey(key) == false) {
                                se = Instantiate(this.pfSoundElement, this.transform);
                                se.gameObject.name = "AudioSourceEffects-" + soundName;
                                se.onDestroyAction += () => {
                                    this._effects.Remove(key);
                                };
                                this._effects.Add(key, se);
                            } else {
                                se = this._effects[key];
                            }
                            se.time = Time.time + setup.delta;
                            se.audioSource.volume = setup.volume * this.GetEffectsVolume();
                            se.audioSource.pitch = setup.pitch;
                            if (setup.spacialParent != null) {
                                se.audioSource.transform.SetParent(setup.spacialParent);
                                se.audioSource.transform.localPosition = Vector3.zero;
                                se.audioSource.maxDistance = setup.spacialMaxDistance;
                                se.audioSource.spatialBlend = 1f;
                                se.audioSource.rolloffMode = AudioRolloffMode.Custom;
                            }
                            if (setup.loop == true) {
                                se.audioSource.clip = clip;
                                se.audioSource.loop = setup.loop;
                                se.audioSource.Play();
                            } else {
                                se.audioSource.PlayOneShot(clip);
                            }
                            return se.audioSource;
                        }
                    } else {
                        Debug.LogError("[SOUNDMANAGER] EFFECT NOT FOUND " + soundName);
                    }
                }
                return null;
            }

            /// <summary>
            /// Plays a sound effect.
            /// </summary>
            /// <param name="soundName">Name of the sound</param>
            /// <param name="volume">Volume between 0 and 1 included</param>
            /// <param name="pitch">Pitch</param>
            /// <param name="spacialParent">Parent when using spacialized sounds</param>
            /// <param name="spacialMaxDistance">Max distance to hear the sound when using spacialized sounds</param>
            /// <param name="loop">Is the sound looping</param>
            /// <param name="key">Additional key if the sound played needs to be unique</param>
            /// <param name="delta">Delay between each occurence of the same sound played</param>
            /// <returns>Returns the Audiosource handling the sound</returns>
            public AudioSource PlayEffect(string soundName, float volume, float pitch, Transform spacialParent,
                                    float spacialMaxDistance, bool loop = false, string key = "", float delta = 0.03f) {
                SoundEffectSetup setup = new SoundEffectSetup();
                setup.volume = volume;
                setup.pitch = pitch;
                setup.spacialParent = spacialParent;
                setup.spacialMaxDistance = spacialMaxDistance;
                setup.loop = loop;
                setup.key = key;
                setup.delta = delta;
                return this._PlayEffect(soundName, setup);
            }

            /// <summary>
            /// Plays a sound effect.
            /// </summary>
            /// <param name="soundName">Name of the sound</param>
            /// <param name="volume">Volume between 0 and 1 included</param>
            /// <param name="key">Additional key if the sound played needs to be unique</param>
            /// <param name="delta">Delay between each occurence of the same sound played</param>
            /// <returns>Returns the Audiosource handling the sound</returns>
            public AudioSource PlayEffect(string soundName, float volume = 1f, string key = "", float delta = 0.03f) {
                return this.PlayEffect(soundName, volume, 1, null, 10f, false, key, delta);
            }

            /// <summary>
            /// Plays a sound effect.
            /// </summary>
            /// <param name="soundName">Name of the sound</param>
            /// <param name="volume">Volume between 0 and 1 included</param>
            /// <param name="pitch">Pitch</param>
            /// <param name="key">Additional key if the sound played needs to be unique</param>
            /// <param name="delta">Delay between each occurence of the same sound played</param>
            /// <returns>Returns the Audiosource handling the sound</returns>
            public AudioSource PlayEffect(string soundName, float volume, float pitch, string key = "", float delta = 0.03f) {
                return this.PlayEffect(soundName, volume, pitch, null, 10f, false, key, delta);
            }

            /// <summary>
            /// Plays a sound effect.
            /// </summary>
            /// <param name="soundName">Name of the sound</param>
            /// <param name="volume">Volume between 0 and 1 included</param>
            /// <param name="pitch">Pitch</param>
            /// <param name="loop">Is the sound looping</param>
            /// <param name="key">Additional key if the sound played needs to be unique</param>
            /// <param name="delta">Delay between each occurence of the same sound played</param>
            /// <returns>Returns the Audiosource handling the sound</returns>
            public AudioSource PlayEffect(string soundName, float volume, float pitch, bool loop, string key = "", float delta = 0.03f) {
                return this.PlayEffect(soundName, volume, pitch, null, 10f, loop, key, delta);
            }

            /// <summary>
            /// Pauses the given effect
            /// </summary>
            /// <param name="soundName">Name of the sound</param>
            public void PauseEffect(string soundName) {
                if (this._effects.ContainsKey(soundName)) {
                    SoundElement se = this._effects[soundName];
                    if (se.audioSource != null && se.audioSource.loop == true) {
                        se.audioSource.Pause();
                    } else {
                        Debug.LogError("[SOUNDMANAGER] EFFECT NOT FOUND OR NOT LOOPING " + soundName);
                    }
                }
            }

            /// <summary>
            /// Unpauses the given effect
            /// </summary>
            /// <param name="soundName">Name of the sound</param> 
            public void UnPauseEffect(string soundName) {
                if (this._effects.ContainsKey(soundName)) {
                    SoundElement se = this._effects[soundName];
                    if (se.audioSource != null && se.audioSource.loop == true) {
                        se.audioSource.UnPause();
                    } else {
                        Debug.LogError("[SOUNDMANAGER] EFFECT NOT FOUND OR NOT LOOPING " + soundName);
                    }
                }
            }

            /// <summary>
            /// Stops the given effect
            /// </summary>
            /// <param name="soundName">Name of the sound</param> 
            public void StopEffect(string soundName) {
                if (this._effects.ContainsKey(soundName)) {
                    this._effects[soundName].audioSource.Stop();
                } else {
                    Debug.LogError("[SOUNDMANAGER] EFFECT NOT FOUND " + soundName);
                }
            }

            /// <summary>
            /// Get the Audiosource associated with the given effect
            /// </summary>
            /// <param name="soundName">Name of the sound</param>
            /// <returns>Returns the Audiosource handling the sound</returns>
            public AudioSource GetEffectAudioSource(string soundName) {
                if (this._effects.ContainsKey(soundName)) {
                    return this._effects[soundName].audioSource;
                } else {
                    Debug.LogError("[SOUNDMANAGER] EFFECT NOT FOUND " + soundName);
                    return null;
                }
            }

            private void ClearDestroyedEffectObjects() {
                this._effects = this._effects.Where(effect => effect.Value.audioSource != null).ToDictionary(x => x.Key, x => x.Value);
            }

            /// <summary>
            /// Enable or disable the effects setting
            /// </summary>
            public void ToggleEffects() {
                this.ycManager.dataManager.ToggleSoundEffect();
            }

            /// <summary>
            /// Enable or disable sound effects
            /// </summary>
            /// <param name="enable">bool parameter to enable or disable sound effects</param>
            public void EnableEffects(bool enable) {
                this.ycManager.dataManager.SetSoundEffect(enable);
            }

            /// <summary>
            /// Set the volume of the sound effects
            /// </summary>
            /// <param name="volume">The value for the volume</param>
            public void SetEffectsVolume(float volume) {
                this.ycManager.dataManager.SetSoundEffectVolume(volume);
            }

            /// <summary>
            /// Get the volume of the sound effects
            /// </summary>
            public float GetEffectsVolume() {
                return this.ycManager.dataManager.GetSoundEffectVolume();
            }

            /// <summary>
            /// Returns if the sound effects can be played
            /// </summary>
            /// <returns>Returns true if the sound effects can be played</returns>
            public bool AreEffectsEnabled() {
                return this.ycManager.dataManager.GetSoundEffect();
            }


            // MUSIC

            private bool CanMusic() {
                return this.ycManager.ycConfig.SoundMusic == true && this.IsMusicEnabled() && this.GetMusicVolume() > 0;
            }

            /// <summary>
            /// Plays a music
            /// </summary>
            /// <param name="musicName">Name of the music</param>
            /// <param name="volume">Volume between 0 and 1 included</param>
            public void PlayMusic(string musicName, float volume = 0.5f) {
                if (this.CanMusic()) {
                    if (this._audioSourceMusics == null) {
                        this._audioSourceMusics = new GameObject("AudioSourceMusics").AddComponent<AudioSource>();
                        this._audioSourceMusics.transform.SetParent(this.transform);
                        this._audioSourceMusics.loop = true;
                    }
                    this._targetMusicVolume = volume;
                    if (this._rc.musics.ContainsKey(musicName)) {
                        this._audioSourceMusics.clip = this._rc.musics[musicName];
                        this.SetMusicVolume();
                        this._audioSourceMusics.Play();
                    } else {
                        Debug.LogError("[SOUNDMANAGER] MUSIC NOT FOUND " + musicName);
                    }
                    this._audioSourceMusics.clip = this._rc.musics[musicName];
                    this._audioSourceMusics.volume = volume;
                    this._audioSourceMusics.Play();
                }
            }

            /// <summary>
            /// Pauses the currently playing music
            /// </summary>
            public void PauseMusic() {
                if (this._audioSourceMusics != null) {
                    this._audioSourceMusics.Pause();
                }
            }

            /// <summary>
            /// Unpauses the currently playing music
            /// </summary>
            public void UnPauseMusic() {
                if (this._audioSourceMusics != null && this.CanMusic()) {
                    this._audioSourceMusics.UnPause();
                }
            }

            /// <summary>
            /// Stops the currently playing music
            /// </summary>
            public void StopMusic() {
                if (this._audioSourceMusics != null) {
                    this._audioSourceMusics.clip = null;
                }
            }

            /// <summary>
            /// Get the Audiosource handling the music.
            /// </summary>
            /// <returns>Returns the Audiosource handling the music</returns>
            public AudioSource GetMusicAudiosource() {
                return this._audioSourceMusics;
            }

            /// <summary>
            /// Pause or unpause the music depending if the music can be played
            /// </summary>
            public void CheckStartStopMusic() {
                if (this._audioSourceMusics) {
                    if (this.IsMusicEnabled()) {
                        this._audioSourceMusics.UnPause();
                    } else {
                        this._audioSourceMusics.Pause();
                    }
                }
            }

            /// <summary>
            /// Enable or disable the Music setting
            /// </summary>
            public void ToggleMusic() {
                this.ycManager.dataManager.ToggleSoundMusic();
                this.CheckStartStopMusic();
            }

            /// <summary>
            /// Enable or disable music
            /// </summary>
            /// <param name="enable">bool parameter to enable or disable music</param>
            public void EnableMusic(bool enable) {
                this.ycManager.dataManager.SetSoundMusic(enable);
            }

            /// <summary>
            /// Set the volume of the music
            /// </summary>
            /// <param name="volume">The value for the volume</param>
            public void SetMusicVolume(float volume = -1f) {
                if (volume >= 0) {
                    this.ycManager.dataManager.SetSoundMusicVolume(volume);
                }
                if (this._audioSourceMusics != null) {
                    this._audioSourceMusics.volume = this._targetMusicVolume * this.GetMusicVolume();
                }
            }

            /// <summary>
            /// Get the volume of the music
            /// </summary>
            public float GetMusicVolume() {
                return this.ycManager.dataManager.GetSoundMusicVolume();
            }

            /// <summary>
            /// Returns if the music is on
            /// </summary>
            /// <returns>Returns true if the music is on</returns>
            public bool IsMusicEnabled() {
                return this.ycManager.dataManager.GetSoundMusic();
            }
        }
    }
}