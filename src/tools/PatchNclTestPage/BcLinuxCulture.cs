using System;
using System.Collections.Concurrent;
using System.Globalization;

/// <summary>
/// Source of the culture helper the "culture" patch copies into BC's assemblies (issue #113).
///
/// This class is never called from the patcher. PatchCulture reads this assembly with Cecil and
/// copies every method of it into Nav.Ncl.dll / Nav.Types.dll, so it has to stay self-contained:
/// static methods that call only the framework, no lambdas, no closures, no static fields, no
/// try/catch (nothing a plain IL copy cannot carry over).
///
/// What differs between a Windows tier (NLS culture data) and this one (ICU/CLDR):
///  * Every culture: ICU 72+ puts a narrow no-break space (U+202F) before AM/PM in time
///    patterns; NLS uses a plain space. "2:30:05 PM" came out with a character no Windows
///    tier writes, and showed up in Format(), report datasets, CSV length and page values.
///  * da-DK: the short date is dd-MM-yyyy on Windows and dd.MM.y in CLDR.
/// The rest of the ICU/NLS gap (collation, special-casing) is not reachable from here.
/// </summary>
internal static class BcLinuxCulture
{
    internal static CultureInfo Fix(CultureInfo culture)
    {
        if (culture == null || culture.IsNeutralCulture)
            return culture;

        // One fixed clone per culture name. The cache lives in the AppDomain's data slots because
        // a plain IL copy cannot carry a static field over. Two threads racing on the first call
        // each build a cache and one wins; both answers are equal, so nothing depends on which.
        string name = culture.Name;
        ConcurrentDictionary<string, CultureInfo> cache =
            (ConcurrentDictionary<string, CultureInfo>)AppDomain.CurrentDomain.GetData("BcLinux.WindowsCulture");
        if (cache == null)
        {
            cache = new ConcurrentDictionary<string, CultureInfo>();
            AppDomain.CurrentDomain.SetData("BcLinux.WindowsCulture", cache);
        }
        CultureInfo cached;
        if (cache.TryGetValue(name, out cached))
            return cached;

        CultureInfo result = Build(culture);
        cache[name] = result;
        return result;
    }

    private static CultureInfo Build(CultureInfo culture)
    {
        DateTimeFormatInfo source = culture.DateTimeFormat;
        bool danish = culture.Name.StartsWith("da", StringComparison.Ordinal);
        if (!HasNarrowSpace(source) && !danish)
            return culture;

        CultureInfo copy = (CultureInfo)culture.Clone();
        DateTimeFormatInfo d = copy.DateTimeFormat;
        d.ShortTimePattern = PlainSpace(source.ShortTimePattern);
        d.LongTimePattern = PlainSpace(source.LongTimePattern);
        d.ShortDatePattern = PlainSpace(source.ShortDatePattern);
        d.LongDatePattern = PlainSpace(source.LongDatePattern);
        d.FullDateTimePattern = PlainSpace(source.FullDateTimePattern);
        d.YearMonthPattern = PlainSpace(source.YearMonthPattern);
        d.MonthDayPattern = PlainSpace(source.MonthDayPattern);
        d.AMDesignator = PlainSpace(source.AMDesignator);
        d.PMDesignator = PlainSpace(source.PMDesignator);
        if (danish)
        {
            d.DateSeparator = "-";
            d.ShortDatePattern = source.ShortDatePattern.Replace('.', '-');
        }
        return CultureInfo.ReadOnly(copy);
    }

    /// <summary>
    /// Same rules for a culture BC has just constructed with `new CultureInfo(...)`. That instance
    /// is writable and BC fills in some of its patterns itself (NavSession.GetFormattingCulture),
    /// so it is changed in place and returned rather than cloned.
    /// </summary>
    internal static CultureInfo FixWritable(CultureInfo culture)
    {
        if (culture == null || culture.IsNeutralCulture || culture.IsReadOnly)
            return culture;

        DateTimeFormatInfo d = culture.DateTimeFormat;
        d.ShortTimePattern = PlainSpace(d.ShortTimePattern);
        d.LongTimePattern = PlainSpace(d.LongTimePattern);
        d.ShortDatePattern = PlainSpace(d.ShortDatePattern);
        d.LongDatePattern = PlainSpace(d.LongDatePattern);
        d.FullDateTimePattern = PlainSpace(d.FullDateTimePattern);
        d.YearMonthPattern = PlainSpace(d.YearMonthPattern);
        d.MonthDayPattern = PlainSpace(d.MonthDayPattern);
        d.AMDesignator = PlainSpace(d.AMDesignator);
        d.PMDesignator = PlainSpace(d.PMDesignator);
        if (culture.Name.StartsWith("da", StringComparison.Ordinal))
        {
            d.DateSeparator = "-";
            d.ShortDatePattern = d.ShortDatePattern.Replace('.', '-');
        }
        return culture;
    }

    private static bool HasNarrowSpace(DateTimeFormatInfo d)
    {
        return d.ShortTimePattern.IndexOf(' ') >= 0
            || d.LongTimePattern.IndexOf(' ') >= 0
            || d.FullDateTimePattern.IndexOf(' ') >= 0
            || d.AMDesignator.IndexOf(' ') >= 0
            || d.PMDesignator.IndexOf(' ') >= 0;
    }

    private static string PlainSpace(string value)
    {
        return value == null ? null : value.Replace(' ', ' ');
    }
}
