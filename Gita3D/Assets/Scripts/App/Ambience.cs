using UnityEngine;

namespace Gita.App
{
    /// <summary>
    /// The bansuri bed that plays under the whole app.
    ///
    /// It gets out of the way whenever a verse is being read. Narration goes through
    /// Android's speech engine on a stream this has no control over, so the two cannot
    /// be mixed by the platform - this has to watch what the narrator is doing and pull
    /// its own level down. It drops fast when speech starts, so no word is ever competing
    /// with a flute, and comes back slowly afterwards, so the return is not itself a
    /// distraction.
    ///
    /// Music and narration are separate settings on purpose. Someone who wants to read
    /// in silence and someone who wants the bed without a voice are both ordinary.
    /// </summary>
    public sealed class Ambience : MonoBehaviour
    {
        /// <summary>Level when nothing is being spoken.</summary>
        const float Bed = 0.15f;

        /// <summary>
        /// Level while a verse is being read. Not silence: a bed that cuts out entirely
        /// draws more attention to itself than one that steps back.
        /// </summary>
        const float Ducked = 0.022f;

        const float DuckDownPerSecond = 4.0f;   // ~0.25s to get out of the way
        const float DuckUpPerSecond = 0.55f;    // ~1.8s to come back

        public static Ambience Instance { get; private set; }

        AudioSource _source;
        float _poll;
        bool _speaking;

        public static Ambience Create(Transform parent)
        {
            var go = new GameObject("~Ambience");
            go.transform.SetParent(parent, false);
            var a = go.AddComponent<Ambience>();
            a.Init();
            return a;
        }

        void Init()
        {
            Instance = this;

            var clip = Resources.Load<AudioClip>("Audio/bansuri-ambience");
            if (clip == null)
            {
                Debug.LogWarning("[Gita] Ambience clip missing - continuing without music.");
                return;
            }

            _source = gameObject.AddComponent<AudioSource>();
            _source.clip = clip;
            _source.loop = true;
            _source.playOnAwake = false;
            _source.volume = 0f;          // faded in by Update
            _source.spatialBlend = 0f;    // 2D: it is not coming from anywhere in the set
            _source.priority = 200;       // yield to anything else that wants a voice
            _source.bypassEffects = true;
            _source.bypassListenerEffects = true;

            if (AppSettings.MusicEnabled) _source.Play();
        }

        /// <summary>Starts or stops the bed. Called when the setting changes.</summary>
        public void Refresh()
        {
            if (_source == null) return;

            if (AppSettings.MusicEnabled)
            {
                if (!_source.isPlaying) _source.Play();
            }
            else if (_source.isPlaying && _source.volume <= 0.001f)
            {
                _source.Stop();
            }
        }

        void Update()
        {
            if (_source == null) return;

            // Asking the speech engine is a call across the JNI boundary; four times a
            // second is far more often than a duck needs and still costs nothing.
            _poll -= Time.unscaledDeltaTime;
            if (_poll <= 0f)
            {
                _poll = 0.25f;
                _speaking = Narration.IsSpeaking;
            }

            float target = !AppSettings.MusicEnabled ? 0f
                         : _speaking ? Ducked
                         : Bed;

            float rate = target < _source.volume ? DuckDownPerSecond : DuckUpPerSecond;
            _source.volume = Mathf.MoveTowards(_source.volume, target, rate * Time.unscaledDeltaTime);

            // Only stop once it has actually faded out, so turning music off is a fade
            // rather than a cut.
            if (!AppSettings.MusicEnabled && _source.isPlaying && _source.volume <= 0.001f)
                _source.Stop();
            else if (AppSettings.MusicEnabled && !_source.isPlaying)
                _source.Play();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
