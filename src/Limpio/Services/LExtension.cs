using System.Windows.Markup;

namespace Limpio.Services;

/// <summary>XAML markup extension returning a localized string: {svc:L home.title}.</summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class LExtension : MarkupExtension
{
    public string Key { get; set; }

    public LExtension(string key) => Key = key;

    public override object ProvideValue(IServiceProvider serviceProvider) => Loc.T(Key);
}
