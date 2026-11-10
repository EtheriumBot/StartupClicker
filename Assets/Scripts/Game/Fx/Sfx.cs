using UnityEngine;

namespace StartupClicker.Game
{
    /// <summary>Plays one-shot sound effects with a little random pitch so repeats don't sound identical.</summary>
    public sealed class Sfx : MonoBehaviour
    {
        static Sfx instance;
        AudioSource[] sources;
        int next;

        public static void Play(string name, float volume = 0.6f, float pitch = 1f, float pitchJitter = 0.06f)
        {
            if (instance == null)
            {
                var go = new GameObject("Sfx");
                instance = go.AddComponent<Sfx>();
                instance.sources = new AudioSource[6];
                for (int i = 0; i < instance.sources.Length; i++)
                {
                    instance.sources[i] = go.AddComponent<AudioSource>();
                    instance.sources[i].playOnAwake = false;
                }
            }
            var clip = SpriteBank.Clip(name);
            if (clip == null) return;
            // several sources in rotation so fast clicks overlap instead of cutting each other off
            var src = instance.sources[instance.next];
            instance.next = (instance.next + 1) % instance.sources.Length;
            src.pitch = pitch + Random.Range(-pitchJitter, pitchJitter);
            src.PlayOneShot(clip, volume);
        }
    }
}
