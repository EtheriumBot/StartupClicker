using System;
using System.Collections.Generic;
using UnityEngine;

namespace StartupClicker.Game
{
    /// <summary>
    /// A very small animation helper. Tween.Run(0.3f, t => ...) calls your function every frame
    /// with t going from 0 to 1 over 0.3 seconds. Easing functions bend t to make motion feel
    /// snappy instead of robotic.
    /// </summary>
    public sealed class Tween : MonoBehaviour
    {
        sealed class Job
        {
            public float Duration, Elapsed;
            public Action<float> Step;
            public Action Done;
            public Func<float, float> Ease;
        }

        static Tween runner;
        readonly List<Job> jobs = new List<Job>();

        public static void Run(float duration, Action<float> step, Action done = null, Func<float, float> ease = null)
        {
            if (runner == null)
            {
                var go = new GameObject("Tweens");
                DontDestroyOnLoad(go);
                runner = go.AddComponent<Tween>();
            }
            runner.jobs.Add(new Job { Duration = Mathf.Max(0.0001f, duration), Step = step, Done = done, Ease = ease ?? Linear });
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = jobs.Count - 1; i >= 0; i--)
            {
                var j = jobs[i];
                j.Elapsed += dt;
                float t = Mathf.Clamp01(j.Elapsed / j.Duration);
                j.Step(j.Ease(t));
                if (t >= 1f)
                {
                    jobs.RemoveAt(i);
                    if (j.Done != null) j.Done();
                }
            }
        }

        public static float Linear(float t) { return t; }

        /// <summary>Fast start, gentle stop.</summary>
        public static float OutCubic(float t) { t = 1f - t; return 1f - t * t * t; }

        /// <summary>Overshoots slightly past the target and settles back - gives a "pop".</summary>
        public static float OutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            t -= 1f;
            return 1f + c3 * t * t * t + c1 * t * t;
        }
    }

    /// <summary>
    /// Squash-and-stretch on a spring. Punch() gives the object a kick; a damped spring then pulls
    /// its scale back to normal, wobbling a little on the way. This is most of what makes clicking
    /// feel good.
    /// </summary>
    public sealed class PunchScale : MonoBehaviour
    {
        public float Stiffness = 420f;
        public float Damping = 16f;
        Vector3 baseScale = Vector3.one;
        float offset, velocity;
        bool hasBase;

        public void SetBase(Vector3 scale)
        {
            baseScale = scale;
            hasBase = true;
            Apply();
        }

        public void Punch(float strength)
        {
            if (!hasBase) { baseScale = transform.localScale; hasBase = true; }
            velocity += strength * 22f;
            offset = Mathf.Clamp(offset, -0.3f, 0.3f);
        }

        void Update()
        {
            if (Mathf.Abs(offset) < 0.0005f && Mathf.Abs(velocity) < 0.0005f) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
            velocity += (-Stiffness * offset - Damping * velocity) * dt;
            offset += velocity * dt;
            Apply();
        }

        void Apply()
        {
            // taller + thinner one way, shorter + wider the other: volume looks preserved
            transform.localScale = new Vector3(baseScale.x * (1f - offset * 0.6f), baseScale.y * (1f + offset), baseScale.z);
        }
    }
}
