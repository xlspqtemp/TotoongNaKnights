using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Plays sliced Human Snapshot activity frames and listens to the independent routine activity broadcast.
/// </summary>
public class HumanSnapshotAnimator : MonoBehaviour
{
    private const string SPRITE_RESOURCE_PATH = "UI/Human Snapshot/image-removebg-preview (8)";
    private const int FRAMES_PER_ACTIVITY = 8;

    [Header("Display")]
    [SerializeField] private Image displayImage;

    [Header("Playback")]
    [Min(0.1f)]
    [SerializeField] private float framesPerSecond = 8f;
    [Min(0f)]
    [SerializeField] private float activityHoldDuration = 2.5f;

    private readonly Dictionary<RoutineActivity, Sprite[]> activityFrames = new Dictionary<RoutineActivity, Sprite[]>();
    private readonly HashSet<RoutineActivity> warnedActivities = new HashSet<RoutineActivity>();
    private Coroutine playbackRoutine;
    private RoutineActivity currentActivity = RoutineActivity.Idle;

    private void Awake()
    {
        if (displayImage == null)
            displayImage = GetComponent<Image>();

        LoadActivityFrames();
    }

    private void OnEnable()
    {
        RoutineSystem.OnRoutineActivityChanged += HandleRoutineActivityChanged;
    }

    private void Start()
    {
        PlayActivity(RoutineActivity.Idle);
    }

    private void OnDisable()
    {
        RoutineSystem.OnRoutineActivityChanged -= HandleRoutineActivityChanged;

        if (playbackRoutine != null)
        {
            StopCoroutine(playbackRoutine);
            playbackRoutine = null;
        }
    }

    /// <summary>
    /// Immediately switches to the requested activity when art is available.
    /// Non-idle activities return to Idle after the configured hold duration.
    /// </summary>
    public void PlayActivity(RoutineActivity activity)
    {
        Sprite[] frames;
        if (!TryGetValidFrames(activity, out frames))
            return;

        currentActivity = activity;
        if (playbackRoutine != null)
            StopCoroutine(playbackRoutine);

        playbackRoutine = StartCoroutine(PlayFrames(activity, frames));
    }

    private void HandleRoutineActivityChanged(RoutineActivity activity)
    {
        PlayActivity(activity);
    }

    private IEnumerator PlayFrames(RoutineActivity activity, Sprite[] frames)
    {
        float frameDuration = 1f / Mathf.Max(0.1f, framesPerSecond);
        float holdTimer = 0f;
        int frameIndex = 0;

        while (true)
        {
            if (displayImage != null && frames.Length > 0)
                displayImage.sprite = frames[frameIndex];

            frameIndex = (frameIndex + 1) % frames.Length;
            yield return new WaitForSecondsRealtime(frameDuration);

            if (activity == RoutineActivity.Idle)
                continue;

            holdTimer += frameDuration;
            if (holdTimer >= activityHoldDuration)
            {
                currentActivity = RoutineActivity.Idle;
                Sprite[] idleFrames;
                if (TryGetValidFrames(RoutineActivity.Idle, out idleFrames))
                {
                    playbackRoutine = StartCoroutine(PlayFrames(RoutineActivity.Idle, idleFrames));
                }
                else
                {
                    playbackRoutine = null;
                }

                yield break;
            }
        }
    }

    private void LoadActivityFrames()
    {
        Sprite[] sprites = Resources.LoadAll<Sprite>(SPRITE_RESOURCE_PATH);
        Dictionary<string, List<Sprite>> groupedSprites = new Dictionary<string, List<Sprite>>(StringComparer.OrdinalIgnoreCase);

        foreach (Sprite sprite in sprites)
        {
            string activityName;
            int frameIndex;
            if (!TryParseSpriteName(sprite.name, out activityName, out frameIndex))
                continue;

            List<Sprite> frames;
            if (!groupedSprites.TryGetValue(activityName, out frames))
            {
                frames = new List<Sprite>();
                groupedSprites.Add(activityName, frames);
            }

            while (frames.Count <= frameIndex)
                frames.Add(null);
            frames[frameIndex] = sprite;
        }

        foreach (RoutineActivity activity in Enum.GetValues(typeof(RoutineActivity)))
        {
            string activityName = GetSpriteActivityName(activity);
            if (string.IsNullOrEmpty(activityName))
                continue;

            List<Sprite> frames;
            if (!groupedSprites.TryGetValue(activityName, out frames))
                continue;

            Sprite[] frameSet = frames.ToArray();
            if (frameSet.Length == FRAMES_PER_ACTIVITY)
                activityFrames[activity] = frameSet;
        }
    }

    private bool TryGetValidFrames(RoutineActivity activity, out Sprite[] frames)
    {
        if (activityFrames.TryGetValue(activity, out frames) &&
            frames != null &&
            frames.Length == FRAMES_PER_ACTIVITY &&
            Array.TrueForAll(frames, frame => frame != null))
        {
            return true;
        }

        if (warnedActivities.Add(activity))
        {
            Debug.LogWarning($"[HumanSnapshotAnimator] No complete 8-frame art set exists for routine activity '{activity}'. Keeping the current snapshot animation.");
        }

        frames = null;
        return false;
    }

    private bool TryParseSpriteName(string spriteName, out string activityName, out int frameIndex)
    {
        string[] nameParts = spriteName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (nameParts.Length != 2)
        {
            activityName = null;
            frameIndex = -1;
            return false;
        }

        activityName = nameParts[0];
        frameIndex = string.Equals(nameParts[1], "Default", StringComparison.OrdinalIgnoreCase)
            ? 0
            : ParseFrameNumber(nameParts[1]);
        return frameIndex >= 0 && frameIndex < FRAMES_PER_ACTIVITY;
    }

    private int ParseFrameNumber(string frameName)
    {
        int frameNumber;
        return int.TryParse(frameName, out frameNumber) ? frameNumber : -1;
    }

    private string GetSpriteActivityName(RoutineActivity activity)
    {
        switch (activity)
        {
            case RoutineActivity.Idle: return "Idle";
            case RoutineActivity.CommutingToWorkOrSchool: return "Walk";
            case RoutineActivity.Exercising: return "Jog";
            case RoutineActivity.LeisureTime:
            case RoutineActivity.Relaxing: return "Sit";
            case RoutineActivity.EatingBreakfast:
            case RoutineActivity.EatingLunch:
            case RoutineActivity.EatingJunkFood:
            case RoutineActivity.EatingDinner: return "Eat";
            case RoutineActivity.Sleeping: return "Sleep";
            case RoutineActivity.WorkingOrStudying: return "Work";
            default: return null;
        }
    }
}
