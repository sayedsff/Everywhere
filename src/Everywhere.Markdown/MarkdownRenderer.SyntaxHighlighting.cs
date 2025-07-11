// @author https://github.com/SlimeNull
// @author https://github.com/AuroraZiling
// @author https://github.com/DearVa

using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using ColorCode;
using ColorCode.Common;
using ColorCode.Parsing;
using ColorCode.Styling;

namespace Everywhere.Markdown;

public partial class MarkdownRenderer
{
    private class SyntaxHighlighting(InlineCollection inlines, StyleDictionary? styles = null, ILanguageParser? languageParser = null)
        : CodeColorizerBase(styles, languageParser)
    {
        public void FormatInlines(string sourceCode, ILanguage language)
        {
            languageParser.Parse(sourceCode, language, Write);
        }

        protected override void Write(string parsedSourceCode, IList<Scope> scopes)
        {
            var styleInsertions = new List<TextInsertion>();

            foreach (var scope in scopes)
                GetStyleInsertionsForCapturedStyle(scope, styleInsertions);

            styleInsertions.SortStable((x, y) => x.Index.CompareTo(y.Index));

            var offset = 0;

            Scope? previousScope = null;

            foreach (var styleInsertion in styleInsertions)
            {
                var text = parsedSourceCode.Substring(offset, styleInsertion.Index - offset);
                CreateSpan(text, previousScope);
                if (!string.IsNullOrWhiteSpace(styleInsertion.Text))
                {
                    CreateSpan(text, previousScope);
                }
                offset = styleInsertion.Index;

                previousScope = styleInsertion.Scope;
            }

            var remaining = parsedSourceCode[offset..];
            // Ensures that those loose carriages don't run away!
            if (remaining != "\r")
            {
                CreateSpan(remaining, null);
            }
        }

        private void CreateSpan(string text, Scope? scope)
        {
            var span = new Span();
            var run = new Run
            {
                Text = text
            };

            // Styles and writes the text to the span.
            if (scope != null)
                StyleRun(run, scope);
            span.Inlines.Add(run);

            inlines.Add(span);
        }

        private void StyleRun(Run run, Scope scope)
        {
            if (!Styles.TryGetValue(scope.Name, out var style)) return;

            if (Color.TryParse(style.Foreground, out var color))
            {
                run.Foreground = new SolidColorBrush(color);
            }

            if (Color.TryParse(style.Background, out var backgroundColor))
            {
                run.Background = new SolidColorBrush(backgroundColor);
            }

            if (style.Italic)
                run.FontStyle = FontStyle.Italic;

            if (style.Bold)
                run.FontWeight = FontWeight.Bold;
        }

        private static void GetStyleInsertionsForCapturedStyle(Scope scope, ICollection<TextInsertion> styleInsertions)
        {
            styleInsertions.Add(
                new TextInsertion
                {
                    Index = scope.Index,
                    Scope = scope
                });

            foreach (var childScope in scope.Children)
                GetStyleInsertionsForCapturedStyle(childScope, styleInsertions);

            styleInsertions.Add(
                new TextInsertion
                {
                    Index = scope.Index + scope.Length
                });
        }
    }
}