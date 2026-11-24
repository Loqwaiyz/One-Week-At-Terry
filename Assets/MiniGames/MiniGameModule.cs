using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Terry.MiniGames
{
    public static class MiniGameModule
    {
        public static bool HasGame(int day) => day >= 1 && day <= 3;

        // Campaign play uses the saved scene, including its prefab overrides.
        public static void LoadAfterDay(int day, Action onCompleted)
        {
            if (!HasGame(day)) throw new ArgumentOutOfRangeException(nameof(day));
            if (onCompleted == null) throw new ArgumentNullException(nameof(onCompleted));
            var load = SceneManager.LoadSceneAsync($"Assets/MiniGames/Day{day:00}ParkWalk.unity");
            load.completed += _ => UnityEngine.Object.FindFirstObjectByType<ParkWalk>().OnCompleted = onCompleted;
        }

        // The day controller owns the transition and suspends its own controls.
        public static ParkWalk PlayAfterDay(int day, Action onCompleted)
        {
            if (!HasGame(day))
                throw new ArgumentOutOfRangeException(nameof(day), day, "Для этого дня нет мини-игры.");
            if (onCompleted == null)
                throw new ArgumentNullException(nameof(onCompleted));
            if (UnityEngine.Object.FindFirstObjectByType<ParkWalk>() != null)
                throw new InvalidOperationException("Прогулка уже запущена.");

            var prefab = Resources.Load<GameObject>($"TerryParkWalk/Day{day:00}Theatre").GetComponent<ParkWalk>();
            // Stage the performance away from the host level, within its own camera's short clip range.
            var walk = UnityEngine.Object.Instantiate(prefab, new Vector3(0, -1000, 0), Quaternion.identity);
            walk.OnCompleted = onCompleted;
            return walk;
        }
    }
}

