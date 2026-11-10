using UnityEngine;

namespace StartupClicker.Game
{
    /// <summary>Sprays a handful of angular shards that fly out, fall and fade. Used for clicks and purchases.</summary>
    public static class ShardBurst
    {
        public static void Spawn(Vector3 position, Color color, int count = 7, float power = 1f, int sortingOrder = 20000)
        {
            var sprite = SpriteBank.Centered("fx_shard");
            if (sprite == null) return;
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Shard");
                go.transform.position = position;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingOrder = sortingOrder;
                sr.color = color;

                float angle = Random.Range(20f, 160f) * Mathf.Deg2Rad;
                Vector3 velocity = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * Random.Range(1.6f, 3.6f) * power;
                float spin = Random.Range(-540f, 540f);
                float size = Random.Range(0.7f, 1.5f) * power;
                Vector3 start = position;
                var tr = go.transform;
                tr.rotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));
                float life = Random.Range(0.35f, 0.6f);

                Tween.Run(life, t =>
                {
                    if (tr == null) return;
                    float time = t * life;
                    // simple ballistic arc: initial velocity plus gravity
                    tr.position = start + velocity * time + new Vector3(0f, -6f * time * time, 0f);
                    tr.Rotate(0, 0, spin * Time.unscaledDeltaTime);
                    tr.localScale = Vector3.one * size * (1f - t * 0.6f);
                    var c = color;
                    c.a = 1f - t * t;
                    sr.color = c;
                }, () => { if (go != null) Object.Destroy(go); });
            }
        }
    }
}
