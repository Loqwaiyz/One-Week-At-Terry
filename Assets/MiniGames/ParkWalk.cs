using System;
using UnityEngine;

namespace Terry.MiniGames
{
    public sealed class ParkWalk : MonoBehaviour
    {
        [Header("Сцена")]
        [SerializeField] Transform child;
        [SerializeField] Camera stageCamera;
        [SerializeField] Renderer tent, entrance, grotto;
        [SerializeField] Transform ball;
        [SerializeField] Transform[] notes;
        [Header("Прогулка")]
        [SerializeField, Min(0.1f)] float walkSpeed = 3.4f;
        [SerializeField, Min(0.1f)] float watchRadius = 3.4f;
        [Header("Звук")]
        [UnityEngine.Serialization.FormerlySerializedAs("music")]
        [SerializeField] AudioSource showAudio;
        [SerializeField] AudioSource backgroundMusic;
        [SerializeField] AudioClip[] melodies;

        Vector3 spawn, childScale, ballStart;
        Quaternion childRotation;
        Vector3[] noteStarts;
        const float ViewWidth = 1000f;
        const float ViewHeight = 600f;
        const float WatchTime = 1.4f;
        const float FadeDuration = 1.5f;
        const float FinalPause = 2.25f;
        float normalVolume, musicGain = 1, fadeStartsAt;
        float Fade => finished ? Mathf.SmoothStep(0, 1, (ending - fadeStartsAt) / FadeDuration) : 0;

        internal Action OnCompleted { private get; set; }

        readonly bool[] watched = new bool[2];
        readonly string[] names = { "Цирковой шатёр", "Центральный вход", "Логово Черепахи" };
        static readonly Color Ink = new Color32(49, 32, 28, 255);
        static readonly Color Paper = new Color32(255, 226, 171, 255);
        static readonly Color Gold = new Color32(246, 178, 78, 255);
        GUIStyle label;
        float gait, facing = 1, watchSeconds, ending;
        int activeShow = -1;
        bool finished;
        int Count => (watched[0] ? 1 : 0) + (watched[1] ? 1 : 0);

        void Start()
        {
            spawn = child.localPosition;
            childScale = child.localScale;
            childRotation = child.localRotation;
            ballStart = ball.localPosition;
            noteStarts = Array.ConvertAll(notes, note => note.localPosition);
            var listener = stageCamera.GetComponent<AudioListener>();
            listener.enabled = !Array.Exists(FindObjectsByType<AudioListener>(FindObjectsSortMode.None),
                other => other != listener && other.isActiveAndEnabled);
            normalVolume = backgroundMusic.volume;
            backgroundMusic.Play();
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (finished) ending += dt;
            float targetGain = showAudio.isPlaying ? 0.23f : 1f;
            musicGain = Mathf.MoveTowards(musicGain, targetGain, dt * (targetGain < musicGain ? 8f : 1.3f));
            backgroundMusic.volume = normalVolume * musicGain * (1 - Fade);
            if (finished)
            {
                if (ending < fadeStartsAt + FadeDuration) return;
                if (backgroundMusic.isPlaying) backgroundMusic.Stop();
                if (OnCompleted != null)
                {
                    var callback = OnCompleted;
                    OnCompleted = null;
                    gameObject.SetActive(false);
                    Destroy(gameObject);
                    callback();
                }
                else if (Input.GetKeyDown(KeyCode.Return))
                {
                    Array.Clear(watched, 0, watched.Length);
                    child.localPosition = spawn;
                    child.localRotation = childRotation;
                    child.localScale = childScale;
                    watchSeconds = ending = gait = 0;
                    musicGain = 1;
                    backgroundMusic.volume = normalVolume;
                    backgroundMusic.Play();
                    activeShow = -1;
                    facing = 1;
                    finished = false;
                }
                return;
            }

            int direction = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1 : 0)
                          - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1 : 0);
            Walk(direction, dt);
        }

        void Walk(int direction, float dt)
        {
            float previous = child.position.x;
            float x = Mathf.Clamp(previous + direction * walkSpeed * dt,
                tent.bounds.center.x - tent.bounds.extents.x * 0.55f, grotto.bounds.center.x + grotto.bounds.extents.x * 0.55f);
            float distance = x - previous;
            if (distance != 0) facing = Mathf.Sign(distance);
            gait = distance == 0 ? 0 : gait + Mathf.Abs(distance) * 4.2f;
            Vector3 pos = child.position;
            pos.x = x;
            pos.y = transform.TransformPoint(spawn).y + Mathf.Abs(Mathf.Sin(gait)) * 0.05f;
            child.position = pos;
            child.localScale = Vector3.Scale(childScale, new Vector3(facing, 1, 1));
            child.localRotation = childRotation * Quaternion.Euler(0, 0, Mathf.Sin(gait) * 2.8f);
            UpdateShow(dt);
        }

        void LateUpdate()
        {
            Vector3 cam = stageCamera.transform.position;
            float halfWidth = (entrance.bounds.center.z - cam.z) *
                Mathf.Tan(stageCamera.fieldOfView * Mathf.Deg2Rad / 2f) * stageCamera.aspect;
            float left = tent.bounds.min.x + halfWidth;
            float right = grotto.bounds.max.x - halfWidth;
            cam.x = left < right ? Mathf.Clamp(child.position.x, left, right) : (left + right) / 2;
            stageCamera.transform.position = cam;

            float t = Time.unscaledTime;
            float phase = Mathf.Repeat(t * 0.78f, 1);
            ball.localPosition = ballStart + new Vector3(
                Mathf.Lerp(-1.18f, 1.18f, Mathf.PingPong(t * 0.78f, 1)),
                5.8f * phase * (1 - phase), 0);
            ball.localRotation = Quaternion.Euler(0, 0, -t * 115f);
            for (int i = 0; i < notes.Length; i++)
            {
                phase = Mathf.Repeat(t * 0.6f + i / (float)notes.Length, 1);
                notes[i].localPosition = noteStarts[i] + new Vector3(1.2f * phase, 1.5f * phase, 0);
                notes[i].localRotation = Quaternion.Euler(0, 0, 10 * Mathf.Sin(phase * Mathf.PI * 2));
            }
        }

        void UpdateShow(float dt)
        {
            float x = child.position.x;
            int show = Mathf.Abs(x - tent.bounds.center.x) < watchRadius ? 0 :
                       Mathf.Abs(x - grotto.bounds.center.x) < watchRadius ? 1 : -1;
            if (show != activeShow)
            {
                activeShow = show;
                watchSeconds = 0;
            }
            if (show < 0 || watched[show]) return;
            watchSeconds += dt;
            if (watchSeconds < WatchTime) return;
            watched[show] = true;
            showAudio.PlayOneShot(melodies[show], 0.55f);
            finished = Count == 2;
            if (finished)
                fadeStartsAt = Mathf.Max(FinalPause, melodies[show].length - FadeDuration + 0.4f);
        }

        static void Fill(Rect rect, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        void Caption(Rect rect, string text, int size, Color color, TextAnchor anchor)
        {
            label.fontSize = size;
            label.alignment = anchor;
            label.normal.textColor = color;
            GUI.Label(rect, text, label);
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || child == null) return;
            label ??= new GUIStyle(GUI.skin.label) { padding = new RectOffset() };
            GUI.depth = -100;
            Matrix4x4 original = GUI.matrix;
            Color originalColor = GUI.color;
            GUI.color = Color.white;

            float scale = Mathf.Min(Screen.width / ViewWidth, Screen.height / ViewHeight);
            float width = Screen.width / scale;
            float height = Screen.height / scale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            Fill(new Rect(0, 0, width, 55), Ink);
            Fill(new Rect(0, height - 65, width, 65), Ink);
            GUI.BeginGroup(new Rect((width - ViewWidth) / 2, 0, ViewWidth, height));
            DrawTicket(height);
            GUI.EndGroup();
            if (finished)
            {
                float fade = Fade;
                Fill(new Rect(0, 0, width, height), new Color(Ink.r, Ink.g, Ink.b, fade));
                if (fade > 0.92f)
                {
                    Caption(new Rect(0, height / 2 - 55, width, 64), "Вторая ночь", 44, Paper, TextAnchor.MiddleCenter);
                    if (OnCompleted == null)
                        Caption(new Rect(0, height / 2 + 20, width, 40), "Enter   ·   Прогуляться ещё раз", 19, Gold, TextAnchor.MiddleCenter);
                }
            }
            GUI.matrix = original;
            GUI.color = originalColor;
        }

        void DrawTicket(float height)
        {
            float x = child.position.x;
            int area = x < (tent.bounds.center.x + entrance.bounds.center.x) / 2 ? 0 :
                       x > (grotto.bounds.center.x + entrance.bounds.center.x) / 2 ? 2 : 1;
            Caption(new Rect(22, 0, 250, 55), "ПАРК ТЕРРИ   /   ДЕНЬ 1", 17, Gold, TextAnchor.MiddleLeft);
            Caption(new Rect(280, 0, 440, 55), names[area], 28, Paper, TextAnchor.MiddleCenter);
            Caption(new Rect(730, 0, 248, 55), $"Выступления    {Count} / 2", 19, Paper, TextAnchor.MiddleRight);
            string hint = "Посмотри оба выступления";
            if (finished)
                hint = "Оба выступления просмотрены";
            else if (activeShow >= 0)
                hint = activeShow == 0 ? "Терри жонглирует под аплодисменты!" : "Ларри играет для гостей на гармони!";
            else if (Count == 1)
                hint = watched[0] ? "Логово Черепахи   →" : "←   Цирковой шатёр";
            else if (area == 1)
                hint = "←   Цирковой шатёр       ·       Логово Черепахи   →";
            Fill(new Rect(0, height - 65, ViewWidth, 2), Gold);
            Caption(new Rect(22, height - 60, 300, 54), "Ходьба   A / D   или   ← / →", 18, Gold, TextAnchor.MiddleLeft);
            Caption(new Rect(315, height - 60, 663, 54), hint, 20, Paper, TextAnchor.MiddleRight);
            if (activeShow >= 0 && !watched[activeShow])
                Fill(new Rect(0, height - 65, ViewWidth * Mathf.Clamp01(watchSeconds / WatchTime), 4), Paper);
        }
    }
}


