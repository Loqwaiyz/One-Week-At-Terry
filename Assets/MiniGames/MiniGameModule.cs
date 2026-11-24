using System;
using UnityEngine;

namespace Terry.MiniGames
{
    public static class MiniGameModule
    {
        // The day controller owns the transition and suspends its own controls.
        public static ParkWalk PlayAfterDay(int day, Action onCompleted)
        {
            if (day != 1 && day != 2)
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

