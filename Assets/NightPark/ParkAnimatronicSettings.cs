using System;
using UnityEngine;

// Add to the ParkRunController object to override the story cast for this scene.
[DisallowMultipleComponent]
public sealed class ParkAnimatronicSettings : MonoBehaviour
{
    public bool useCustomSettings;
    public Profile custom = new Profile();

    [Serializable]
    public sealed class Profile
    {
        [Header("Терри")]
        public bool terry = true;
        [Min(0)] public float terryStart = 24;
        [Min(1)] public int terryStepMin = 16, terryStepMax = 21;
        [Min(0.1f)] public float terryReaction = 6;

        [Header("Ларри")]
        public bool larry = true;
        [Min(0)] public float larryStart = 48, sabotageInterval = 115, guardSeconds = 7, retrySeconds = 8;
        [Min(0)] public int sabotageLimit = 3;
        public bool targetViewedCamera;

        [Header("Уми")]
        public bool umi;
        [Min(0)] public float umiStart = 65, umiApproach = 9, umiReaction = 7;
        [Min(1)] public int umiIntervalMin = 60, umiIntervalMax = 80;

        [Header("Кеша")]
        public bool kesha, avoidViewedCamera;
        [Min(0)] public float keshaStart = 100, keshaInterval = 85;
        [Min(0.1f)] public float searchSeconds = 20, watchSeconds = 4;
        [Min(1)] public int outagesForBrokenFeed = 4;

        [Header("Савелий, Клык и Год")]
        public bool falcon, wolf, elephant;
        [Min(0)] public float visitStart = 30, visitInterval = 50, approachStep = 7;
        [Min(0.1f)] public float falconReaction = 7, wolfReaction = 9, wolfEscape = 24;
        [Min(0.1f)] public float elephantReaction = 18, elephantHide = 3, elephantFollowup = 9;

        public bool HasVisitors => falcon || wolf || elephant;

        public static Profile FromLevels(int[] levels)
        {
            // Menu order: Terry, Larry, Umi, Kesha, Falcon, Wolf, Elephant.
            float Pace(int level) => Mathf.Lerp(1.4f, .7f, (level - 1) / 19f);
            var p = new Profile {
                terry = levels[0] > 0, larry = levels[1] > 0, umi = levels[2] > 0,
                kesha = levels[3] > 0, falcon = levels[4] > 0,
                wolf = levels[5] > 0, elephant = levels[6] > 0
            };
            p.terryStart *= Pace(levels[0]);
            p.terryStepMin = Mathf.RoundToInt(p.terryStepMin * Pace(levels[0]));
            p.terryStepMax = Mathf.RoundToInt(p.terryStepMax * Pace(levels[0]));
            p.larryStart *= Pace(levels[1]);
            p.sabotageInterval *= Pace(levels[1]);
            p.sabotageLimit = 1 + levels[1] / 3;
            p.umiStart *= Pace(levels[2]);
            p.umiReaction *= Pace(levels[2]);
            p.keshaStart *= Pace(levels[3]);
            p.searchSeconds *= Pace(levels[3]);
            p.falconReaction *= Pace(levels[4]);
            p.wolfReaction *= Pace(levels[5]);
            p.elephantReaction *= Pace(levels[6]);
            return p;
        }

        public static Profile Story(int night)
        {
            var p = new Profile {
                umi = night >= 2, kesha = night >= 3, falcon = night >= 5,
                wolf = night >= 7, elephant = night >= 7,
                sabotageLimit = night >= 6 ? 6 : night >= 4 ? 5 : night >= 3 ? 4 : 3
            };
            if (night >= 4)
            {
                p.terryStart = 17;
                p.terryStepMin = night >= 6 ? 9 : 11;
                p.terryStepMax = night >= 6 ? 14 : 16;
                p.terryReaction = 4.5f;
                p.larryStart = 35;
                p.sabotageInterval = night >= 6 ? 70 : 80;
                p.targetViewedCamera = p.avoidViewedCamera = true;
                p.umiStart = 52;
                p.umiApproach = 7;
                p.umiReaction = 5;
                p.umiIntervalMin = 45;
                p.umiIntervalMax = 60;
                p.keshaStart = 80;
                p.keshaInterval = night >= 6 ? 55 : 62;
                p.visitInterval = night >= 6 ? 39 : 50;
            }
            return p;
        }
    }
}
