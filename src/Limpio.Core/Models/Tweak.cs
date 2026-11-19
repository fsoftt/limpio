using System.Text.Json.Serialization;
using Limpio.Core.Actions;

namespace Limpio.Core.Models;

public sealed class Tweak
{
    public string Id { get; set; } = "";
    public Category Category { get; set; }
    public LocalizedText Title { get; set; } = new();
    public LocalizedText Description { get; set; } = new();
    public LocalizedText? Note { get; set; }
    public Risk Risk { get; set; }
    public List<Preset> Presets { get; set; } = new();
    public List<TweakAction> Actions { get; set; } = new();

    public bool IsInPreset(Preset preset) => Presets.Contains(preset);
}
