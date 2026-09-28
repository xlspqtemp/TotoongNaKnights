using System;
using UnityEngine;

/// <summary>Difficulty tiers for the independent daily routine schedules.</summary>
public enum RoutineDifficulty
{
    Easy,
    Normal,
    Medium,
    Hard
}

/// <summary>Generic daily activities that can drive future character presentation changes.</summary>
public enum RoutineActivity
{
    Sleeping,
    WakingUp,
    EatingBreakfast,
    Bathing,
    CommutingToWorkOrSchool,
    WorkingOrStudying,
    LeisureTime,
    EatingLunch,
    EatingJunkFood,
    Exercising,
    EatingDinner,
    Relaxing,
    Idle,
    Playing
}

/// <summary>One scheduled routine transition expressed as an in-game hour and activity.</summary>
[Serializable]
public class RoutineScheduleEntry
{
    [Range(0, 23)]
    public int hour;
    public RoutineActivity activity;

    public RoutineScheduleEntry(int hour, RoutineActivity activity)
    {
        this.hour = hour;
        this.activity = activity;
    }
}

/// <summary>Difficulty-specific list of routine transitions editable in the inspector.</summary>
[Serializable]
public class RoutineDifficultySchedule
{
    public RoutineDifficulty difficulty;
    public RoutineScheduleEntry[] entries;

    public RoutineDifficultySchedule(RoutineDifficulty difficulty, RoutineScheduleEntry[] entries)
    {
        this.difficulty = difficulty;
        this.entries = entries;
    }
}
