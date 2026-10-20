using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Pathogen categories used by dispatch infection entries.</summary>
public enum InfectionPathogenType
{
    Bacterial,
    Viral,
    Fungal
}

/// <summary>Spawn-pool frequency category for one pathogen profile.</summary>
public enum InfectionRarity
{
    Common,
    Uncommon,
    Rare,
    VeryRare
}

/// <summary>Untreated infection severity stages.</summary>
public enum InfectionSeverityStage
{
    Mild,
    Moderate,
    Severe
}

/// <summary>Immune responder categories assigned to infection entries.</summary>
public enum InfectionCorrectResponder
{
    WBC,
    KillerT,
    BCell
}

/// <summary>Body-part groups represented by the existing tactical buttons.</summary>
public enum InfectionBodyPartGroup
{
    Feet,
    Hands,
    Head
}

/// <summary>Pathogen profile data used by the timed infection pool.</summary>
[Serializable]
public sealed class InfectionData
{
    public string displayName;
    public string pathogenDatabaseName;
    public InfectionPathogenType pathogenType;
    public InfectionRarity rarity;
    public InfectionSeverityStage severityStage;
    public int resolveGain;
    [Min(0)] public int neglectLoss;
    public float easySpawnWeight;
    public float normalSpawnWeight;
    public float mediumSpawnWeight;
    public float hardSpawnWeight;
    public bool isGeneric;
    public string entryCause;
    public InfectionCorrectResponder correctResponder;
    public List<InfectionBodyPartGroup> preferredBodyParts = new List<InfectionBodyPartGroup>();
    [Min(0)] public int daysUntreated;
}
