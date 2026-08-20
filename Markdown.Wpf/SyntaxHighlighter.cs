namespace MarkdownView.Wpf;

internal enum SyntaxTokenKind
{
    Text,
    Keyword,
    String,
    Comment,
    Number,
    Literal,
    Variable,
    Command,
    Operator,
}

internal readonly struct SyntaxToken
{
    internal SyntaxToken(string text, SyntaxTokenKind kind)
    {
        Text = text;
        Kind = kind;
    }

    internal string Text { get; }
    internal SyntaxTokenKind Kind { get; }
}

internal static class SyntaxHighlighter
{
    private const int MaxSourceLength = 200_000;
    private const int MaxTokenCount = 12_000;

    private static readonly HashSet<string> JavaScriptKeywords = Words(
        "as async await break case catch class const continue debugger default delete do else export extends finally for from function get if import in instanceof let new of return set static super switch this throw try typeof var void while with yield");

    private static readonly HashSet<string> JavaScriptLiterals = Words(
        "false Infinity NaN null true undefined");

    private static readonly HashSet<string> SqlKeywords = Words(
        "add all alter and any as asc backup begin between by case check column commit constraint create cross database default delete desc distinct drop else end except exists foreign from full function grant group having in index inner insert intersect into is join key left like limit merge not offset on or order outer primary procedure references right rollback row rows schema select set table then top transaction trigger truncate union unique update use values view when where with");

    private static readonly HashSet<string> SqlLiterals = Words(
        "false null true unknown");

    private static readonly HashSet<string> PowerShellKeywords = Words(
        "begin break catch class continue data define do dynamicparam else elseif end enum exit filter finally for foreach from function hidden if in inline parallel param process return static switch throw trap try until using var while workflow");

    private static readonly HashSet<string> PowerShellOperators = Words(
        "-and -as -band -bnot -bor -bxor -contains -creplace -eq -f -ge -gt -icontains -ieq -ige -igt -ile -ilike -ilt -imatch -in -ine -inotcontains -inotlike -inotmatch -is -isnot -join -le -like -lt -match -ne -not -notcontains -notin -notlike -notmatch -or -replace -shl -shr -split -xor");

    private static readonly HashSet<string> BashKeywords = Words(
        "case coproc do done elif else esac fi for function if in select then time until while");

    private static readonly HashSet<string> BashCommands = Words(
        "alias bg bind break builtin caller cd command compgen complete continue declare dirs disown echo enable eval exec exit export false fc fg getopts hash help history jobs kill let local logout mapfile popd printf pushd pwd read readarray readonly return set shift shopt source suspend test times trap true type typeset ulimit umask unalias unset wait");

    private static readonly HashSet<string> CmdKeywords = Words(
        "assoc break call cd chcp chdir cls color copy date del dir echo else endlocal erase exit for ftype goto if md mkdir mklink move path pause popd prompt pushd rd rem ren rename rmdir set setlocal shift start time title type ver verify vol where");

    internal static bool TryTokenize(string source, string? info, out IReadOnlyList<SyntaxToken> tokens)
    {
        tokens = Array.Empty<SyntaxToken>();
        var language = NormalizeLanguage(info);
        if (language is null || source.Length > MaxSourceLength)
            return false;

        var builder = new TokenBuilder(source, MaxTokenCount);
        switch (language)
        {
            case "javascript":
                TokenizeJavaScript(source, builder);
                break;
            case "sql":
                TokenizeSql(source, builder);
                break;
            case "powershell":
                TokenizePowerShell(source, builder);
                break;
            case "bash":
                TokenizeBash(source, builder);
                break;
            case "cmd":
                TokenizeCmd(source, builder);
                break;
            case "markup":
                TokenizeMarkup(source, builder);
                break;
            default:
                return false;
        }

        if (!builder.Success)
            return false;
        tokens = builder.Tokens;
        return true;
    }

    private static string? NormalizeLanguage(string? info)
    {
        if (string.IsNullOrWhiteSpace(info))
            return null;
        var value = info!.Trim();
        var separator = value.IndexOfAny(new[] { ' ', '\t', ',' });
        if (separator >= 0)
            value = value.Substring(0, separator);
        value = value.Trim('{', '}', '.').ToLowerInvariant();
        return value switch
        {
            "bash" or "sh" or "shell" or "zsh" => "bash",
            "js" or "javascript" or "node" or "nodejs" => "javascript",
            "sql" or "mysql" or "postgres" or "postgresql" or "pgsql" or "sqlite" or "tsql" or "mssql" or "plsql" => "sql",
            "cmd" or "bat" or "batch" or "dos" => "cmd",
            "powershell" or "ps1" or "pwsh" => "powershell",
            "html" or "htm" or "xhtml" or "xml" or "svg" => "markup",
            _ => null,
        };
    }

    private static void TokenizeJavaScript(string source, TokenBuilder builder)
    {
        var plain = 0;
        for (var index = 0; index < source.Length;)
        {
            var end = index;
            var kind = SyntaxTokenKind.Text;
            if (StartsWith(source, index, "//"))
            {
                end = LineEnd(source, index);
                kind = SyntaxTokenKind.Comment;
            }
            else if (StartsWith(source, index, "/*"))
            {
                end = BlockEnd(source, index + 2, "*/");
                kind = SyntaxTokenKind.Comment;
            }
            else if (source[index] is '\'' or '"' or '`')
            {
                end = QuotedEnd(source, index, source[index], '\\', false);
                kind = SyntaxTokenKind.String;
            }
            else if (char.IsDigit(source[index]))
            {
                end = NumberEnd(source, index);
                kind = SyntaxTokenKind.Number;
            }
            else if (IsIdentifierStart(source[index]))
            {
                end = IdentifierEnd(source, index, false);
                var word = source.Substring(index, end - index);
                if (JavaScriptKeywords.Contains(word))
                    kind = SyntaxTokenKind.Keyword;
                else if (JavaScriptLiterals.Contains(word))
                    kind = SyntaxTokenKind.Literal;
            }
            else if (IsOperator(source[index]))
            {
                end = index + 1;
                kind = SyntaxTokenKind.Operator;
            }

            if (end == index || kind == SyntaxTokenKind.Text)
            {
                index = end > index ? end : index + 1;
                continue;
            }
            builder.AddPlain(plain, index);
            builder.Add(index, end, kind);
            plain = end;
            index = end;
        }
        builder.AddPlain(plain, source.Length);
    }

    private static void TokenizeSql(string source, TokenBuilder builder)
    {
        var plain = 0;
        for (var index = 0; index < source.Length;)
        {
            var end = index;
            var kind = SyntaxTokenKind.Text;
            if (StartsWith(source, index, "--"))
            {
                end = LineEnd(source, index);
                kind = SyntaxTokenKind.Comment;
            }
            else if (StartsWith(source, index, "/*"))
            {
                end = BlockEnd(source, index + 2, "*/");
                kind = SyntaxTokenKind.Comment;
            }
            else if (source[index] is '\'' or '"' or '`')
            {
                end = QuotedEnd(source, index, source[index], '\0', true);
                kind = SyntaxTokenKind.String;
            }
            else if (source[index] == '[')
            {
                end = BlockEnd(source, index + 1, "]");
                kind = SyntaxTokenKind.String;
            }
            else if (char.IsDigit(source[index]))
            {
                end = NumberEnd(source, index);
                kind = SyntaxTokenKind.Number;
            }
            else if ((source[index] is '@' or ':' or '$') && index + 1 < source.Length && IsIdentifierPart(source[index + 1]))
            {
                end = IdentifierEnd(source, index + 1, false);
                kind = SyntaxTokenKind.Variable;
            }
            else if (IsIdentifierStart(source[index]))
            {
                end = IdentifierEnd(source, index, false);
                var word = source.Substring(index, end - index);
                if (SqlKeywords.Contains(word))
                    kind = SyntaxTokenKind.Keyword;
                else if (SqlLiterals.Contains(word))
                    kind = SyntaxTokenKind.Literal;
            }
            else if (IsOperator(source[index]))
            {
                end = index + 1;
                kind = SyntaxTokenKind.Operator;
            }

            if (end == index || kind == SyntaxTokenKind.Text)
            {
                index = end > index ? end : index + 1;
                continue;
            }
            builder.AddPlain(plain, index);
            builder.Add(index, end, kind);
            plain = end;
            index = end;
        }
        builder.AddPlain(plain, source.Length);
    }

    private static void TokenizePowerShell(string source, TokenBuilder builder)
    {
        var plain = 0;
        for (var index = 0; index < source.Length;)
        {
            var end = index;
            var kind = SyntaxTokenKind.Text;
            if (StartsWith(source, index, "<#"))
            {
                end = BlockEnd(source, index + 2, "#>");
                kind = SyntaxTokenKind.Comment;
            }
            else if (source[index] == '#')
            {
                end = LineEnd(source, index);
                kind = SyntaxTokenKind.Comment;
            }
            else if (index + 1 < source.Length && source[index] == '@' && source[index + 1] is '\'' or '"')
            {
                var terminator = source[index + 1] + "@";
                end = BlockEnd(source, index + 2, terminator);
                kind = SyntaxTokenKind.String;
            }
            else if (source[index] is '\'' or '"')
            {
                end = QuotedEnd(source, index, source[index], '`', source[index] == '\'');
                kind = SyntaxTokenKind.String;
            }
            else if (source[index] == '$')
            {
                end = PowerShellVariableEnd(source, index);
                var value = source.Substring(index, end - index);
                kind = value.Equals("$true", StringComparison.OrdinalIgnoreCase) ||
                    value.Equals("$false", StringComparison.OrdinalIgnoreCase) ||
                    value.Equals("$null", StringComparison.OrdinalIgnoreCase)
                        ? SyntaxTokenKind.Literal
                        : SyntaxTokenKind.Variable;
            }
            else if (char.IsDigit(source[index]))
            {
                end = NumberEnd(source, index);
                kind = SyntaxTokenKind.Number;
            }
            else if (source[index] == '-' && index + 1 < source.Length && char.IsLetter(source[index + 1]))
            {
                end = IdentifierEnd(source, index + 1, true);
                if (PowerShellOperators.Contains(source.Substring(index, end - index)))
                    kind = SyntaxTokenKind.Operator;
            }
            else if (IsIdentifierStart(source[index]))
            {
                end = IdentifierEnd(source, index, true);
                var word = source.Substring(index, end - index);
                if (PowerShellKeywords.Contains(word))
                    kind = SyntaxTokenKind.Keyword;
                else if (word.IndexOf('-') > 0)
                    kind = SyntaxTokenKind.Command;
            }
            else if (IsOperator(source[index]))
            {
                end = index + 1;
                kind = SyntaxTokenKind.Operator;
            }

            if (end == index || kind == SyntaxTokenKind.Text)
            {
                index = end > index ? end : index + 1;
                continue;
            }
            builder.AddPlain(plain, index);
            builder.Add(index, end, kind);
            plain = end;
            index = end;
        }
        builder.AddPlain(plain, source.Length);
    }

    private static void TokenizeBash(string source, TokenBuilder builder)
    {
        var plain = 0;
        for (var index = 0; index < source.Length;)
        {
            var end = index;
            var kind = SyntaxTokenKind.Text;
            if (source[index] == '#' && (index == 0 || char.IsWhiteSpace(source[index - 1])))
            {
                end = LineEnd(source, index);
                kind = SyntaxTokenKind.Comment;
            }
            else if (source[index] is '\'' or '"' or '`')
            {
                end = QuotedEnd(source, index, source[index], source[index] == '\'' ? '\0' : '\\', false);
                kind = SyntaxTokenKind.String;
            }
            else if (source[index] == '$')
            {
                end = BashVariableEnd(source, index);
                kind = SyntaxTokenKind.Variable;
            }
            else if (char.IsDigit(source[index]))
            {
                end = NumberEnd(source, index);
                kind = SyntaxTokenKind.Number;
            }
            else if (IsIdentifierStart(source[index]))
            {
                end = IdentifierEnd(source, index, false);
                var word = source.Substring(index, end - index);
                if (BashKeywords.Contains(word))
                    kind = SyntaxTokenKind.Keyword;
                else if (BashCommands.Contains(word))
                    kind = SyntaxTokenKind.Command;
            }
            else if (IsOperator(source[index]))
            {
                end = index + 1;
                kind = SyntaxTokenKind.Operator;
            }

            if (end == index || kind == SyntaxTokenKind.Text)
            {
                index = end > index ? end : index + 1;
                continue;
            }
            builder.AddPlain(plain, index);
            builder.Add(index, end, kind);
            plain = end;
            index = end;
        }
        builder.AddPlain(plain, source.Length);
    }

    private static void TokenizeCmd(string source, TokenBuilder builder)
    {
        var plain = 0;
        for (var index = 0; index < source.Length;)
        {
            var end = index;
            var kind = SyntaxTokenKind.Text;
            if (IsLinePrefixWhitespace(source, index) && StartsWith(source, index, "::"))
            {
                end = LineEnd(source, index);
                kind = SyntaxTokenKind.Comment;
            }
            else if (IsLinePrefixWhitespace(source, index) && IsWordAt(source, index, "rem"))
            {
                end = LineEnd(source, index);
                kind = SyntaxTokenKind.Comment;
            }
            else if (IsLinePrefixWhitespace(source, index) && source[index] == ':' && !StartsWith(source, index, "::"))
            {
                end = LineEnd(source, index);
                kind = SyntaxTokenKind.Command;
            }
            else if (source[index] == '"')
            {
                end = QuotedEnd(source, index, '"', '^', false);
                kind = SyntaxTokenKind.String;
            }
            else if (source[index] is '%' or '!')
            {
                end = CmdVariableEnd(source, index);
                kind = SyntaxTokenKind.Variable;
            }
            else if (char.IsDigit(source[index]))
            {
                end = NumberEnd(source, index);
                kind = SyntaxTokenKind.Number;
            }
            else if (IsIdentifierStart(source[index]))
            {
                end = IdentifierEnd(source, index, false);
                if (CmdKeywords.Contains(source.Substring(index, end - index)))
                    kind = SyntaxTokenKind.Keyword;
            }
            else if (IsOperator(source[index]) || source[index] == '^')
            {
                end = Math.Min(source.Length, index + (source[index] == '^' && index + 1 < source.Length ? 2 : 1));
                kind = SyntaxTokenKind.Operator;
            }

            if (end == index || kind == SyntaxTokenKind.Text)
            {
                index = end > index ? end : index + 1;
                continue;
            }
            builder.AddPlain(plain, index);
            builder.Add(index, end, kind);
            plain = end;
            index = end;
        }
        builder.AddPlain(plain, source.Length);
    }

    private static void TokenizeMarkup(string source, TokenBuilder builder)
    {
        var plain = 0;
        for (var index = 0; index < source.Length;)
        {
            if (StartsWith(source, index, "<!--"))
            {
                var end = BlockEnd(source, index + 4, "-->");
                builder.AddPlain(plain, index);
                builder.Add(index, end, SyntaxTokenKind.Comment);
                plain = end;
                index = end;
                continue;
            }
            if (StartsWith(source, index, "<![CDATA["))
            {
                var end = BlockEnd(source, index + 9, "]]>");
                builder.AddPlain(plain, index);
                builder.Add(index, end, SyntaxTokenKind.String);
                plain = end;
                index = end;
                continue;
            }
            if (StartsWithIgnoreCase(source, index, "<!DOCTYPE"))
            {
                var end = BlockEnd(source, index + 2, ">");
                builder.AddPlain(plain, index);
                builder.Add(index, end, SyntaxTokenKind.Keyword);
                plain = end;
                index = end;
                continue;
            }
            if (StartsWith(source, index, "<?"))
            {
                var end = BlockEnd(source, index + 2, "?>");
                builder.AddPlain(plain, index);
                builder.Add(index, end, SyntaxTokenKind.Keyword);
                plain = end;
                index = end;
                continue;
            }
            if (source[index] == '<' && index + 1 < source.Length &&
                (source[index + 1] == '/' || IsMarkupNameStart(source[index + 1])))
            {
                builder.AddPlain(plain, index);
                index = TokenizeMarkupTag(source, index, builder);
                plain = index;
                continue;
            }
            if (source[index] == '&')
            {
                var semicolon = source.IndexOf(';', index + 1);
                if (semicolon > index && semicolon - index <= 16)
                {
                    builder.AddPlain(plain, index);
                    builder.Add(index, semicolon + 1, SyntaxTokenKind.Literal);
                    plain = semicolon + 1;
                    index = semicolon + 1;
                    continue;
                }
            }
            index++;
        }
        builder.AddPlain(plain, source.Length);
    }

    private static int TokenizeMarkupTag(string source, int start, TokenBuilder builder)
    {
        var index = start;
        var punctuationEnd = index + 1;
        if (punctuationEnd < source.Length && source[punctuationEnd] == '/')
            punctuationEnd++;
        builder.Add(index, punctuationEnd, SyntaxTokenKind.Operator);
        index = punctuationEnd;

        var nameEnd = MarkupNameEnd(source, index);
        builder.Add(index, nameEnd, SyntaxTokenKind.Keyword);
        index = nameEnd;
        while (index < source.Length)
        {
            if (char.IsWhiteSpace(source[index]))
            {
                var end = index + 1;
                while (end < source.Length && char.IsWhiteSpace(source[end]))
                    end++;
                builder.Add(index, end, SyntaxTokenKind.Text);
                index = end;
            }
            else if (StartsWith(source, index, "/>"))
            {
                builder.Add(index, index + 2, SyntaxTokenKind.Operator);
                return index + 2;
            }
            else if (source[index] == '>')
            {
                builder.Add(index, index + 1, SyntaxTokenKind.Operator);
                return index + 1;
            }
            else if (source[index] is '\'' or '"')
            {
                var end = QuotedEnd(source, index, source[index], '\0', false);
                builder.Add(index, end, SyntaxTokenKind.String);
                index = end;
            }
            else if (source[index] == '=')
            {
                builder.Add(index, index + 1, SyntaxTokenKind.Operator);
                index++;
            }
            else if (IsMarkupNameStart(source[index]))
            {
                var end = MarkupNameEnd(source, index);
                builder.Add(index, end, SyntaxTokenKind.Variable);
                index = end;
            }
            else
            {
                builder.Add(index, index + 1, SyntaxTokenKind.Text);
                index++;
            }
        }
        return index;
    }

    private static int QuotedEnd(string source, int start, char quote, char escape, bool doubledQuote)
    {
        for (var index = start + 1; index < source.Length; index++)
        {
            if (escape != '\0' && source[index] == escape && index + 1 < source.Length)
            {
                index++;
                continue;
            }
            if (source[index] != quote)
                continue;
            if (doubledQuote && index + 1 < source.Length && source[index + 1] == quote)
            {
                index++;
                continue;
            }
            return index + 1;
        }
        return source.Length;
    }

    private static int PowerShellVariableEnd(string source, int start)
    {
        var index = start + 1;
        if (index < source.Length && source[index] == '{')
            return BlockEnd(source, index + 1, "}");
        if (index < source.Length && "_?$^".IndexOf(source[index]) >= 0)
            return index + 1;
        while (index < source.Length && (IsIdentifierPart(source[index]) || source[index] == ':'))
            index++;
        return Math.Max(start + 1, index);
    }

    private static int BashVariableEnd(string source, int start)
    {
        var index = start + 1;
        if (index < source.Length && source[index] == '{')
            return BlockEnd(source, index + 1, "}");
        if (index < source.Length && (char.IsDigit(source[index]) || "_?#!$*@-".IndexOf(source[index]) >= 0))
            return index + 1;
        while (index < source.Length && IsIdentifierPart(source[index]))
            index++;
        return Math.Max(start + 1, index);
    }

    private static int CmdVariableEnd(string source, int start)
    {
        var delimiter = source[start];
        if (delimiter == '%' && start + 1 < source.Length && (char.IsDigit(source[start + 1]) || source[start + 1] == '*'))
            return start + 2;
        if (delimiter == '%' && start + 1 < source.Length && source[start + 1] == '~')
        {
            var index = start + 2;
            while (index < source.Length && (char.IsLetterOrDigit(source[index]) || source[index] == '$' || source[index] == ':'))
                index++;
            return index;
        }
        var close = source.IndexOf(delimiter, start + 1);
        return close >= 0 ? close + 1 : start + 1;
    }

    private static int NumberEnd(string source, int start)
    {
        var index = start;
        while (index < source.Length && (char.IsLetterOrDigit(source[index]) || source[index] is '.' or '_' or '+' or '-'))
            index++;
        return index;
    }

    private static int IdentifierEnd(string source, int start, bool allowHyphen)
    {
        var index = start;
        while (index < source.Length && (IsIdentifierPart(source[index]) || allowHyphen && source[index] == '-'))
            index++;
        return index;
    }

    private static int MarkupNameEnd(string source, int start)
    {
        var index = start;
        while (index < source.Length && (char.IsLetterOrDigit(source[index]) || source[index] is '_' or '-' or ':' or '.'))
            index++;
        return index;
    }

    private static int LineEnd(string source, int start)
    {
        var end = source.IndexOf('\n', start);
        return end < 0 ? source.Length : end;
    }

    private static int BlockEnd(string source, int contentStart, string terminator)
    {
        var end = source.IndexOf(terminator, contentStart, StringComparison.Ordinal);
        return end < 0 ? source.Length : end + terminator.Length;
    }

    private static bool IsLinePrefixWhitespace(string source, int index)
    {
        for (var current = index - 1; current >= 0 && source[current] != '\n'; current--)
            if (!char.IsWhiteSpace(source[current]))
                return false;
        return true;
    }

    private static bool IsWordAt(string source, int index, string value) =>
        index + value.Length <= source.Length &&
        string.Compare(source, index, value, 0, value.Length, StringComparison.OrdinalIgnoreCase) == 0 &&
        (index + value.Length == source.Length || char.IsWhiteSpace(source[index + value.Length]));

    private static bool StartsWith(string source, int index, string value) =>
        index + value.Length <= source.Length &&
        string.CompareOrdinal(source, index, value, 0, value.Length) == 0;

    private static bool StartsWithIgnoreCase(string source, int index, string value) =>
        index + value.Length <= source.Length &&
        string.Compare(source, index, value, 0, value.Length, StringComparison.OrdinalIgnoreCase) == 0;

    private static bool IsIdentifierStart(char value) => char.IsLetter(value) || value == '_';
    private static bool IsIdentifierPart(char value) => char.IsLetterOrDigit(value) || value == '_';
    private static bool IsMarkupNameStart(char value) => char.IsLetter(value) || value is '_' or ':';
    private static bool IsOperator(char value) => "+-*/%=!<>&|?:~(){}[];,".IndexOf(value) >= 0;

    private static HashSet<string> Words(string value) =>
        new(value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);

    private sealed class TokenBuilder
    {
        private readonly string _source;
        private readonly int _maxTokens;
        private readonly List<SyntaxToken> _tokens = new();

        internal TokenBuilder(string source, int maxTokens)
        {
            _source = source;
            _maxTokens = maxTokens;
        }

        internal bool Success { get; private set; } = true;
        internal IReadOnlyList<SyntaxToken> Tokens => _tokens;

        internal void AddPlain(int start, int end) => Add(start, end, SyntaxTokenKind.Text);

        internal void Add(int start, int end, SyntaxTokenKind kind)
        {
            if (!Success || end <= start)
                return;
            if (_tokens.Count >= _maxTokens)
            {
                Success = false;
                _tokens.Clear();
                return;
            }
            _tokens.Add(new SyntaxToken(_source.Substring(start, end - start), kind));
        }
    }
}
