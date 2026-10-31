using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace MySharp.Formatting;

/// <summary>Whitespace-only formatting of fluent expressions in explicitly marked methods.</summary>
public static class CompactFormatter
{
    public static string Format(string source, int width = 120)
    {
        if (width < 40) throw new ArgumentOutOfRangeException(nameof(width), "Width must be at least 40.");
        var root = Parse(source);
        var text = SourceText.From(source);
        var changes = new List<TextChange>();
        foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>().Where(IsComponent))
        {
            foreach (var call in method.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (!Eligible(call)) continue;
                // Format the whole eligible expression once, including any nested argument calls.
                if (call.Ancestors().TakeWhile(n => n != method).OfType<InvocationExpressionSyntax>().Any(Eligible)) continue;
                var line = text.Lines.GetLineFromPosition(call.SpanStart);
                var prefix = source[line.Start..call.SpanStart];
                var indent = new string(prefix.TakeWhile(c => c is ' ' or '\t').ToArray());
                var compact = Compact(call);
                string replacement;
                if (prefix.Length + compact.Length <= width)
                    replacement = compact;
                else
                {
                    // Wrap only at fluent member boundaries, never inside strings or argument lists.
                    var parts = Segments(call);
                    if (parts.Count < 2) continue;
                    var eol = source.Contains("\r\n") ? "\r\n" : "\n";
                    replacement = parts[0];
                    var column = prefix.Length + parts[0].Length;
                    foreach (var part in parts.Skip(1))
                    {
                        if (column + part.Length > width)
                        {
                            replacement += eol + indent + "    ";
                            column = indent.Length + 4;
                        }
                        replacement += part;
                        column += part.Length;
                    }
                }
                if (replacement != source[call.Span.Start..call.Span.End])
                    changes.Add(new TextChange(call.Span, replacement));
            }
        }
        var result = text.WithChanges(changes).ToString();
        var after = Parse(result);
        if (!root.DescendantTokens().Select(t => (t.RawKind, t.Text))
            .SequenceEqual(after.DescendantTokens().Select(t => (t.RawKind, t.Text))))
            throw new InvalidOperationException("Formatting changed tokens; refusing to write.");
        return result;
    }

    private static SyntaxNode Parse(string source)
    {
        var root = CSharpSyntaxTree.ParseText(source).GetRoot();
        var errors = root.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        if (errors.Length != 0) throw new ArgumentException(string.Join("\n", errors.Select(e => e.ToString())));
        return root;
    }

    private static bool IsComponent(MethodDeclarationSyntax method) => method.AttributeLists
        .SelectMany(list => list.Attributes).Any(attribute => attribute.Name.ToString() is
            "UiComponent" or "UiComponentAttribute" or "MySharp.UiComponent" or "MySharp.UiComponentAttribute" or
            "Component" or "ComponentAttribute" or "MySharp.Component" or "MySharp.ComponentAttribute");

    private static bool Eligible(InvocationExpressionSyntax call)
    {
        if (call.Expression is not MemberAccessExpressionSyntax { Expression: InvocationExpressionSyntax }) return false;
        if (call.DescendantNodes().Any(n => n is AnonymousFunctionExpressionSyntax)) return false;
        if (call.DescendantTokens().Any(t => t.Text.Contains('\n') || t.Text.Contains('\r'))) return false;
        return !call.DescendantTrivia().Any(t => !t.IsKind(SyntaxKind.WhitespaceTrivia) && !t.IsKind(SyntaxKind.EndOfLineTrivia));
    }

    private static string Compact(SyntaxNode node) => node.WithoutLeadingTrivia().WithoutTrailingTrivia()
        .NormalizeWhitespace(indentation: "    ", eol: " ").ToFullString();

    private static List<string> Segments(InvocationExpressionSyntax call)
    {
        if (call.Expression is MemberAccessExpressionSyntax { Expression: InvocationExpressionSyntax receiver } member)
        {
            var parts = Segments(receiver);
            parts.Add("." + Compact(member.Name) + Compact(call.ArgumentList));
            return parts;
        }
        return [Compact(call)];
    }
}
