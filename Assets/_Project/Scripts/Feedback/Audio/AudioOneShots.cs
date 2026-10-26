using UnityEngine;

namespace Conquest.Feedback
{
    /// <summary>
    /// Fire-and-forget sounds from a small pool of audio sources, with pitch variation so
    /// repeated hits never sound identical.
    /// </summary>
    public static class AudioOneShots
    {
        public static int Voices = 16;
        /// <summary>0 = 2D (good for a far isometric camera), 1 = fully positional.</summary>
        public static float SpatialBlend = 0.25f;

        private static AudioSource[] _sources;
        private static int _next;

        public static void PlayRandom(AudioClip[] clips, Vector3 position, float volume, Vector2 pitchRange)
        {
            if (clips == null || clips.Length == 0) return;
            Play(clips[Random.Range(0, clips.Length)], position, volume, Random.Range(pitchRange.x, pitchRange.y));
        }

        public static void Play(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f)
        {
            if (clip == null || volume <= 0f) return;
            AudioSource source = NextSource();
            source.transform.position = position;
            source.clip = clip;
            source.volume = volume;
            source.pitch = pitch;
            source.Play();
        }

        private static AudioSource NextSource()
        {
            if (_sources == null || _sources[0] == null)
            {
                var root = new GameObject("[Audio One-Shots]");
                _sources = new AudioSource[Mathf.Max(1, Voices)];
                for (int i = 0; i < _sources.Length; i++)
                {
                    var voice = new GameObject("Voice");
                    voice.transform.SetParent(root.transform, false);
                    AudioSource source = voice.AddComponent<AudioSource>();
                    source.playOnAwake = false;
                    source.spatialBlend = SpatialBlend;
                    source.rolloffMode = AudioRolloffMode.Linear;
                    source.maxDistance = 60f;
                    _sources[i] = source;
                }
            }

            // Prefer a free voice; otherwise steal the next one in line.
            for (int i = 0; i < _sources.Length; i++)
            {
                AudioSource candidate = _sources[(_next + i) % _sources.Length];
                if (candidate.isPlaying) continue;
                _next = (_next + i + 1) % _sources.Length;
                return candidate;
            }

            AudioSource stolen = _sources[_next];
            _next = (_next + 1) % _sources.Length;
            return stolen;
        }
    }
}
