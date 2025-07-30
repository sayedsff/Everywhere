using System.Collections.Immutable;
using System.Text;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Everywhere.I18N.SourceGenerator;

[Generator]
public class I18NSourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Register the additional file provider for RESX files
        var resxFiles = context.AdditionalTextsProvider
            .Where(file => Path.GetExtension(file.Path).Equals(".resx", StringComparison.OrdinalIgnoreCase) &&
                           Path.GetFileName(file.Path).StartsWith("Strings.", StringComparison.OrdinalIgnoreCase) &&
                           Path.GetDirectoryName(file.Path)?.EndsWith("I18N", StringComparison.OrdinalIgnoreCase) == true)
            .Collect();

        // Register the output source
        context.RegisterSourceOutput(resxFiles, GenerateI18NCode);
    }

    private static void GenerateI18NCode(SourceProductionContext context, ImmutableArray<AdditionalText> resxFiles)
    {
        if (resxFiles.Length == 0)
        {
            return;
        }

        try
        {
            // Group RESX files by base name and locale
            var defaultResxFile = resxFiles.FirstOrDefault(f => Path.GetFileName(f.Path).Equals("Strings.resx", StringComparison.OrdinalIgnoreCase));
            if (defaultResxFile == null)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        new DiagnosticDescriptor(
                            "I18N001",
                            "Missing Default RESX File",
                            "Could not find the default Strings.resx file in I18N directory",
                            "I18N",
                            DiagnosticSeverity.Error,
                            isEnabledByDefault: true),
                        Location.None));
                return;
            }

            // Parse the default RESX to get all keys
            var defaultContent = defaultResxFile.GetText(context.CancellationToken)?.ToString();
            if (string.IsNullOrEmpty(defaultContent))
            {
                return;
            }

            // Parse default RESX for keys and values
            var defaultEntries = ParseResxEntries(defaultContent!);
            if (defaultEntries.Count == 0)
            {
                return;
            }

            // Generate LocaleKey.g.cs based on all available keys
            var localeKeySource = GenerateLocaleKeyClass(defaultResxFile.Path, defaultEntries);
            context.AddSource("LocaleKey.g.cs", SourceText.From(localeKeySource, Encoding.UTF8));

            // Generate default locale file
            var defaultLocaleSource = GenerateLocaleClass(defaultResxFile.Path, "@default", defaultEntries);
            context.AddSource("default.g.cs", SourceText.From(defaultLocaleSource, Encoding.UTF8));

            // Generate locale files for each language-specific RESX
            var localeNames = new List<string>();
            foreach (var resxFile in resxFiles.Where(f => !Path.GetFileName(f.Path).Equals("Strings.resx", StringComparison.OrdinalIgnoreCase)))
            {
                var fileName = Path.GetFileNameWithoutExtension(resxFile.Path);
                var localeName = fileName.Substring(fileName.IndexOf('.') + 1);
                var escapedLocaleName = localeName.Replace("-", "_");
                localeNames.Add(localeName);

                var content = resxFile.GetText(context.CancellationToken)?.ToString();
                if (string.IsNullOrEmpty(content))
                {
                    continue;
                }

                var entries = ParseResxEntries(content!);
                var localeSource = GenerateLocaleClass(resxFile.Path, escapedLocaleName, entries);
                context.AddSource($"{localeName}.g.cs", SourceText.From(localeSource, Encoding.UTF8));
            }

            // Generate LocaleManager.g.cs
            var localeManagerSource = GenerateLocaleManagerClass(defaultResxFile.Path, localeNames);
            context.AddSource("LocaleManager.g.cs", SourceText.From(localeManagerSource, Encoding.UTF8));
        }
        catch (Exception ex)
        {
            // Report diagnostic for any errors
            context.ReportDiagnostic(
                Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "I18N002",
                        "I18N Generation Error",
                        $"Error generating I18N code: {ex.Message}",
                        "I18N",
                        DiagnosticSeverity.Error,
                        isEnabledByDefault: true),
                    Location.None));
        }
    }

    private static Dictionary<string, string> ParseResxEntries(string resxContent)
    {
        var entries = new Dictionary<string, string>();

        try
        {
            var doc = XDocument.Parse(resxContent);
            var dataNodes = doc.Root?.Elements("data");

            if (dataNodes != null)
            {
                foreach (var dataNode in dataNodes)
                {
                    var nameAttr = dataNode.Attribute("name");
                    var valueNode = dataNode.Element("value");

                    if (nameAttr != null && valueNode != null)
                    {
                        entries[nameAttr.Value] = valueNode.Value;
                    }
                }
            }
        }
        catch (Exception)
        {
            // Silently fail and return an empty dictionary
            return new Dictionary<string, string>();
        }

        return entries;
    }

    private static string GenerateLocaleKeyClass(string resxPath, Dictionary<string, string> entries)
    {
        var sb = new StringBuilder();

        sb.AppendLine(
            $$"""
              // Generated by Everywhere.I18N.SourceGenerator, do not edit manually
              // Edit {{Path.GetFileName(resxPath)}} instead, run the generator or build project to update this file

              #nullable enable

              namespace Everywhere.I18N;

              public static class LocaleKey
              {
              """);

        foreach (var entry in entries)
        {
            var key = entry.Key;
            var escapedKey = EscapeVariableName(key);

            sb.AppendLine($"    public const string {escapedKey} = \"{key}\";");
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string GenerateLocaleClass(string resxPath, string escapedLocaleName, Dictionary<string, string> entries)
    {
        var sb = new StringBuilder();

        sb.AppendLine(
            $$"""
              // Generated by Everywhere.I18N.SourceGenerator, do not edit manually
              // Edit {{Path.GetFileName(resxPath)}} instead, run the generator or build project to update this file

              #nullable enable

              namespace Everywhere.I18N;

              public class {{escapedLocaleName}} : global::Avalonia.Controls.ResourceDictionary
              {
                  public {{escapedLocaleName}}()
                  {
              """);

        foreach (var entry in entries)
        {
            var key = entry.Key;
            var value = entry.Value;

            // Escape quotes in the value
            value = value
                .Replace("\"", "\\\"")
                .Replace(Environment.NewLine, "\\n")
                .Replace("\r", "\\n")
                .Replace("\n", "\\n");

            sb.AppendLine($"        Add(\"{key}\", \"{value}\");");
        }

        sb.AppendLine(
            """
                }
            }
            """);

        return sb.ToString();
    }

    private static string GenerateLocaleManagerClass(string resxPath, List<string> localeNames)
    {
        var sb = new StringBuilder();

        sb.AppendLine(
            $$"""
              // Generated by Everywhere.I18N.SourceGenerator, do not edit manually
              // Edit {{Path.GetFileName(resxPath)}} instead, run the generator or build project to update this file

              #nullable enable

              using global::System.Diagnostics.CodeAnalysis;
              using global::Avalonia.Controls;

              namespace Everywhere.I18N;

              public static class LocaleManager
              {
                  public static IEnumerable<string> AvailableLocaleNames => Locales.Keys;

                  private static readonly Dictionary<string, ResourceDictionary> Locales = new();

                  static LocaleManager()
                  {
                      Locales.Add("default", new @default());
              """);

        foreach (var localeName in localeNames)
        {
            var escapedLocaleName = localeName.Replace("-", "_");
            sb.AppendLine($"        Locales.Add(\"{localeName}\", new {escapedLocaleName}());");
        }

        sb.AppendLine(
            """
                }

                public static string? CurrentLocale
                {
                    get => field;
                    set
                    {
                        if (field == value) return;
                
                        var app = Application.Current!;
                        if (field != null && Locales.TryGetValue(field, out var oldLocale))
                        {
                            app.Resources.MergedDictionaries.Remove(oldLocale);
                        }
                        
                        field = value;
                        if (value is null || !Locales.TryGetValue(value, out var newLocale))
                        {
                            (field, newLocale) = Locales.First();
                        }
                        app.Resources.MergedDictionaries.Add(newLocale);
                    }
                }
            }
            """);

        return sb.ToString();
    }

    private static string EscapeVariableName(string s)
    {
        // Replace invalid characters with underscores, and ensure it starts with a letter
        var escaped = new string(s.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
        if (escaped.Length > 0 && char.IsDigit(escaped[0]))
        {
            escaped = "_" + escaped; // Ensure it starts with a letter or underscore
        }
        return escaped;
    }
}