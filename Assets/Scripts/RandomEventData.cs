using System;

/// <summary>Classifies the body system or lifestyle area affected by a random event.</summary>
public enum RandomEventType
{
    Respiratory,
    Digestive,
    GeneralLifestyle,
    Physical,
    Psychological,
    PsychologicalPhysical
}

/// <summary>Severity selected from the active difficulty preset.</summary>
public enum RandomEventSeverity
{
    Minor,
    Moderate,
    Severe
}

/// <summary>Difficulty presets supported by the random event prototype.</summary>
public enum RandomEventDifficulty
{
    Easy,
    Normal,
    Medium,
    Hard
}

/// <summary>Stable identifiers for the sample random event pool.</summary>
public enum RandomEventId
{
    AteExpiredFood,
    InhaledDustOrAllergen,
    SkippedMeal,
    Overexertion,
    StressOrPoorHydration,
    JunkFoodBinge,
    ArgumentOrConflict,
    ColdFromSickPerson,
    SunOrFreshAir,
    NickedOrScraped
}

/// <summary>Weighted reference to one event in a difficulty preset's event pool.</summary>
[Serializable]
public class RandomEventPoolWeight
{
    public RandomEventId eventId;
    public float weight;

    public RandomEventPoolWeight(RandomEventId eventId, float weight)
    {
        this.eventId = eventId;
        this.weight = weight;
    }
}

/// <summary>Difficulty-specific trigger, severity, and event-pool configuration.</summary>
[Serializable]
public class RandomEventDifficultyPreset
{
    public RandomEventDifficulty difficulty;
    public float hourlyTriggerChance;
    public float minorSeverityWeight;
    public float moderateSeverityWeight;
    public float severeSeverityWeight;
    public RandomEventPoolWeight[] eventPoolWeights;

    public RandomEventDifficultyPreset(
        RandomEventDifficulty difficulty,
        float hourlyTriggerChance,
        float minorSeverityWeight,
        float moderateSeverityWeight,
        float severeSeverityWeight,
        RandomEventPoolWeight[] eventPoolWeights)
    {
        this.difficulty = difficulty;
        this.hourlyTriggerChance = hourlyTriggerChance;
        this.minorSeverityWeight = minorSeverityWeight;
        this.moderateSeverityWeight = moderateSeverityWeight;
        this.severeSeverityWeight = severeSeverityWeight;
        this.eventPoolWeights = eventPoolWeights;
    }
}

/// <summary>Payload broadcast when the random event system generates an event.</summary>
public class RandomEventData
{
    public readonly string eventName;
    public readonly string eventDescription;
    public readonly RandomEventType eventType;
    public readonly RandomEventSeverity severity;
    public readonly bool triggersQTE;
    public readonly bool triggersEscalation;
    public readonly float healthImpact;
    public readonly RandomEventDifficulty difficulty;
    public readonly int day;
    public readonly int hour;

    public RandomEventData(
        string eventName,
        string eventDescription,
        RandomEventType eventType,
        RandomEventSeverity severity,
        bool triggersQTE,
        bool triggersEscalation,
        float healthImpact,
        RandomEventDifficulty difficulty,
        int day,
        int hour)
    {
        this.eventName = eventName;
        this.eventDescription = eventDescription;
        this.eventType = eventType;
        this.severity = severity;
        this.triggersQTE = triggersQTE;
        this.triggersEscalation = triggersEscalation;
        this.healthImpact = healthImpact;
        this.difficulty = difficulty;
        this.day = day;
        this.hour = hour;
    }
}
