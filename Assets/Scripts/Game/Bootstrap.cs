using UnityEngine;
using UnityEngine.SceneManagement;

namespace StartupClicker.Game
{
    /// <summary>
    /// Entry point. Unity calls Start() automatically once the first scene has loaded, and it
    /// creates the whole game from code - so there is nothing to wire up in the editor:
    /// open a scene, press Play.
    /// </summary>
    public static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Start()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded; // also rebuild after a scene reload (used by Reset)
            Ensure();
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Ensure();
        }

        static void Ensure()
        {
            if (GameController.Instance != null) return;
            new GameObject("Game").AddComponent<GameController>();
        }
    }
}
