using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Everywhere.I18N.SourceGenerator;

[Generator]
public class I18NSourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Register the additional file provider
        var tsvFiles = context.AdditionalTextsProvider
            .Where(file => Path.GetFileName(file.Path).Equals("i18n.tsv", StringComparison.OrdinalIgnoreCase))
            .Select((file, _) => file);

        // Register the output source
        context.RegisterSourceOutput(tsvFiles, GenerateI18NCode);
    }

    private void GenerateI18NCode(SourceProductionContext context, AdditionalText tsvFile)
    {
        if (tsvFile.GetText(context.CancellationToken) is not { } tsvContent)
        {
            return;
        }

        var filePath = tsvFile.Path;

        try
        {
            // Parse the TSV content
            var tsvRows = ParseTsv(tsvContent.ToString());
            if (tsvRows.Count == 0)
            {
                return;
            }

            var header = tsvRows[0];
            var dataRows = tsvRows.Skip(1).ToList();

            // Generate LocaleKey.g.cs
            var localeKeySource = GenerateLocaleKeyClass(filePath, dataRows);
            context.AddSource("LocaleKey.g.cs", SourceText.From(localeKeySource, Encoding.UTF8));

            // Generate locale files for each column
            for (var col = 1; col < header.Count; col++)
            {
                var localeName = header[col];
                var escapedLocaleName = localeName.Replace("-", "_");
                var localeSource = GenerateLocaleClass(filePath, escapedLocaleName, dataRows, col);
                context.AddSource($"{localeName}.g.cs", SourceText.From(localeSource, Encoding.UTF8));
            }

            // Generate LocaleManager.g.cs
            var localeManagerSource = GenerateLocaleManagerClass(filePath, header.Skip(1).ToList());
            context.AddSource("LocaleManager.g.cs", SourceText.From(localeManagerSource, Encoding.UTF8));
        }
        catch (Exception ex)
        {
            // Report diagnostic for any errors
            context.ReportDiagnostic(
                Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "I18N001",
                        "I18N Generation Error",
                        $"Error generating I18N code: {ex.Message}",
                        "I18N",
                        DiagnosticSeverity.Error,
                        isEnabledByDefault: true),
                    Location.None));
        }
    }

    private static List<List<string>> ParseTsv(string content)
    {
        var rows = new List<List<string>>();
        var currentRow = new List<string>();
        var currentField = new StringBuilder();
        var inQuotes = false;
        var i = 0;

        while (i < content.Length)
        {
            var c = content[i];

            switch (c)
            {
                // Handle quoted fields that can contain newlines
                case '"' when !inQuotes:
                    // Start of a quoted field
                    inQuotes = true;
                    break;
                case '"' when i + 1 < content.Length && content[i + 1] == '"':
                    // Escaped quote inside a quoted field
                    currentField.Append('"');
                    i++; // Skip the next quote character
                    break;
                case '"':
                    // End of quoted field
                    inQuotes = false;
                    break;
                case '\t' when !inQuotes:
                    // Tab outside quotes means end of field
                    currentRow.Add(currentField.ToString());
                    currentField.Clear();
                    break;
                default:
                {
                    if ((c == '\n' || (c == '\r' && i + 1 < content.Length && content[i + 1] == '\n')) && !inQuotes)
                    {
                        // Newline outside quotes means end of row
                        // First add the last field in the row
                        currentRow.Add(currentField.ToString());
                        currentField.Clear();

                        // Add the row if it's not empty
                        if (currentRow.Count > 0)
                        {
                            rows.Add(currentRow);
                            currentRow = [];
                        }

                        // Skip \n if this was \r\n
                        if (c == '\r' && i + 1 < content.Length && content[i + 1] == '\n')
                        {
                            i++;
                        }
                    }
                    else
                    {
                        // Normal character, add to current field
                        currentField.Append(c);
                    }
                    break;
                }
            }

            i++;
        }

        if (currentField.Length <= 0 && currentRow.Count <= 0) return rows;

        // Don't forget the last field and row if there's no newline at the end
        currentRow.Add(currentField.ToString());
        rows.Add(currentRow);

        return rows;
    }

    private static string GenerateLocaleKeyClass(string tsvPath, List<List<string>> rows)
    {
        var sb = new StringBuilder();

        sb.AppendLine(
            $$"""
              // Generated by Everywhere.I18N.SourceGenerator, do not edit manually
              // Edit {{Path.GetFileName(tsvPath)}} instead, run the generator or build project to update this file

              #nullable enable

              namespace Everywhere.I18N;

              public static class LocaleKey
              {
              """);

        foreach (var row in rows)
        {
            if (row.Count == 0) continue;

            var key = row[0];
            var escapedKey = EscapeVariableName(key);

            sb.AppendLine($"    public const string {escapedKey} = \"{key}\";");
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string GenerateLocaleClass(string tsvPath, string escapedLocaleName, List<List<string>> rows, int col)
    {
        var sb = new StringBuilder();

        sb.AppendLine(
            $$"""
              // Generated by Everywhere.I18N.SourceGenerator, do not edit manually
              // Edit {{Path.GetFileName(tsvPath)}} instead, run the generator or build project to update this file

              #nullable enable

              namespace Everywhere.I18N;

              public class {{escapedLocaleName}} : global::Avalonia.Controls.ResourceDictionary
              {
                  public {{escapedLocaleName}}()
                  {
              """);

        foreach (var row in rows)
        {
            if (row.Count == 0 || col >= row.Count) continue;

            var key = row[0];
            var value = row[col];

            // Escape quotes in the value
            value = value.Replace("\"", "\\\"").Replace(Environment.NewLine, "\\n");

            sb.AppendLine($"        Add(\"{key}\", \"{value}\");");
        }

        sb.AppendLine(
            """
                }
            }
            """);

        return sb.ToString();
    }

    private static string GenerateLocaleManagerClass(string tsvPath, List<string> localeNames)
    {
        var sb = new StringBuilder();

        sb.AppendLine(
            $$"""
              // Generated by Everywhere.I18N.SourceGenerator, do not edit manually
              // Edit {{Path.GetFileName(tsvPath)}} instead, run the generator or build project to update this file

              #nullable enable

              using global::System.Diagnostics.CodeAnalysis;
              using global::Avalonia.Controls;

              namespace Everywhere.I18N;

              public static class LocaleManager
              {
                  public static IEnumerable<string> AvailableLocaleNames => Locales.Keys;

                  private static readonly Dictionary<string, ResourceDictionary> Locales = new();
                  private static string? field;

                  static LocaleManager()
                  {
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