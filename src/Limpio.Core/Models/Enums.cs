using System.Text.Json.Serialization;

namespace Limpio.Core.Models;

[JsonConverter(typeof(JsonStringEnumConverter<Category>))]
public enum Category { Apps, Telemetry, Privacy, AI, Ads, Services, ScheduledTasks }

[JsonConverter(typeof(JsonStringEnumConverter<Risk>))]
public enum Risk { Safe, Moderate, Advanced }

[JsonConverter(typeof(JsonStringEnumConverter<Preset>))]
public enum Preset { Minimal, Recommended, Aggressive, Custom }

/// <summary>State of an action or tweak on the machine.</summary>
public enum ActionState
{
    /// <summary>Target exists and is not in the desired state.</summary>
    NotApplied,
    /// <summary>Already in the desired state.</summary>
    Applied,
    /// <summary>Target does not exist on this machine (nothing to do).</summary>
    NotPresent,
}
