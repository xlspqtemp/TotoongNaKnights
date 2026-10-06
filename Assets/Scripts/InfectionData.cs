using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Pathogen categories used by dispatch infection entries.</summary>
public enum InfectionPathogenType
{
    Bacterial,
    Viral
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

/// <summary>Inspector-authored infection data used by the dispatch-style spawner.</summary>
[Serializable]
public sealed class InfectionData
{
    public string displayName;
    public InfectionPathogenType pathogenType;
    public InfectionSeverityStage severityStage;
    public string entryCause;
    public InfectionCorrectResponder correctResponder;
    public List<InfectionBodyPartGroup> preferredBodyParts = new List<InfectionBodyPartGroup>();
    [Min(0)] public int daysUntreated;
}
