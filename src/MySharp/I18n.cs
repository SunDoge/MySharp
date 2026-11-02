using System.Diagnostics;
using System.Globalization;
using System.Resources;
using Avalonia.Media;

namespace MySharp;

/// <summary>Application-owned translations. Changing Culture refreshes views that read this service.</summary>
public sealed class I18n : ObservableState
{
    private readonly Func<string, CultureInfo, string?> lookup;
    private readonly Func<decimal, CultureInfo, string> pluralCategory;
    private readonly HashSet<string> missing = [];
    private CultureInfo culture;
    public I18n(ResourceManager resources, CultureInfo? culture = null, Func<decimal, CultureInfo, string>? pluralCategory = null)
        : this((resources ?? throw new ArgumentNullException(nameof(resources))).GetString, culture, pluralCategory) { }
    /// <summary>Custom lookup implementations should perform their own default-language fallback.</summary>
    public I18n(Func<string, CultureInfo, string?> lookup, CultureInfo? culture = null, Func<decimal, CultureInfo, string>? pluralCategory = null)
    {
        this.lookup = lookup ?? throw new ArgumentNullException(nameof(lookup));
        this.culture = Snapshot(culture ?? CultureInfo.CurrentUICulture);
        this.pluralCategory = pluralCategory ?? DefaultPluralCategory;
    }
    public CultureInfo Culture
    {
        get { Observe(); return culture; }
        set
        {
            Verify(); var next = Snapshot(value);
            if (ReferenceEquals(culture, next)) return;
            culture = next; missing.Clear(); Notify();
        }
    }
    public FlowDirection FlowDirection => Culture.TextInfo.IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
    /// <summary>Raised once per missing key per culture selection; fallback strings are not missing.</summary>
    public event Action<string, CultureInfo>? MissingTranslation;
    public string this[string key]
    {
        get
        {
            ArgumentException.ThrowIfNullOrEmpty(key); var current = Culture;
            var text = lookup(key, current);
            if (text is not null) return text;
            if (missing.Add(key)) { Debug.WriteLine($"Missing translation '{key}' ({current.Name})"); MissingTranslation?.Invoke(key, current); }
            return key;
        }
    }
    public string Format(string key, params object?[] arguments) => string.Format(Culture, this[key], arguments);
    /// <summary>Looks up key.one/key.other/etc. and formats the count as argument zero.</summary>
    public string Plural(string key, decimal count) => Format(key + "." + pluralCategory(count, Culture), count);
    private static CultureInfo Snapshot(CultureInfo value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.IsReadOnly ? value : CultureInfo.ReadOnly((CultureInfo)value.Clone());
    }
    private static string DefaultPluralCategory(decimal count, CultureInfo culture) => culture.TwoLetterISOLanguageName switch
    {
        "en" or "iv" => Math.Abs(count) == 1 && ((decimal.GetBits(count)[3] >> 16) & 0xff) == 0 ? "one" : "other",
        "zh" or "ja" or "ko" => "other",
        _ => throw new NotSupportedException($"Supply a pluralCategory rule for '{culture.Name}' (for example from a CLDR implementation).")
    };
    protected override void DisposeCore() { missing.Clear(); MissingTranslation = null; }
}

public sealed partial class UiContext
{
    /// <summary>Subscribes this view to an application-owned I18n service. Apply its FlowDirection explicitly to the desired subtree.</summary>
    public I18n UseI18n(I18n translations)
    {
        ArgumentNullException.ThrowIfNull(translations);
        _ = translations.Culture;
        return translations;
    }
}
