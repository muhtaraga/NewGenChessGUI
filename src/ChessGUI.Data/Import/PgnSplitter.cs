using System.Text;

namespace ChessGUI.Data.Import;

/// <summary>
/// Çok oyunlu bir PGN akışını tek tek oyun metinlerine böler. Akış tabanlıdır (satır satır okur),
/// bu yüzden gigabaytlık dosyaları düşük bellekle işleyebilir. Oyun sınırı, hamle metninden sonra
/// gelen ilk etiket satırı (<c>[Ad "..."]</c>) ile belirlenir.
/// </summary>
public static class PgnSplitter
{
    /// <summary>Akıştaki her oyunu ayrı bir ham PGN metni olarak sırayla döndürür.</summary>
    public static IEnumerable<string> Split(TextReader reader)
    {
        var buffer = new StringBuilder();
        bool inMoves = false;          // etiketlerden sonra hamle bölümüne geçtik mi
        bool sawTagBlock = false;      // bu oyun için en az bir etiket satırı gördük mü
        bool sawBlankAfterTags = false; // etiketlerden sonra boş satır gördük mü (hamlesiz oyun/bay dahil)
        bool hasContent = false;

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            bool isTag = IsTagLine(line);
            bool isBlank = line.Trim().Length == 0;

            // Yeni bir etiket satırı, önceki oyunun etiket bölümü hamle metniyle ya da (hamlesiz
            // bay/forfeit oyunlarda olduğu gibi) yalnızca boş bir satırla kapanmışsa gelir:
            // önceki oyun burada biter.
            if (isTag && sawTagBlock && (inMoves || sawBlankAfterTags))
            {
                yield return buffer.ToString();
                buffer.Clear();
                inMoves = false;
                sawTagBlock = false;
                sawBlankAfterTags = false;
                hasContent = false;
            }

            if (isTag) sawTagBlock = true;
            else if (isBlank) { if (sawTagBlock && !inMoves) sawBlankAfterTags = true; }
            else inMoves = true;

            if (isTag || !isBlank || hasContent)
            {
                buffer.Append(line).Append('\n');
                hasContent = true;
            }
        }

        if (buffer.ToString().Trim().Length > 0)
            yield return buffer.ToString();
    }

    /// <summary>Bir dosyayı akış olarak böler.</summary>
    public static IEnumerable<string> SplitFile(string path)
    {
        using var reader = new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        foreach (string game in Split(reader))
            yield return game;
    }

    private static bool IsTagLine(string line)
    {
        int i = 0;
        while (i < line.Length && char.IsWhiteSpace(line[i])) i++;
        if (i >= line.Length || line[i] != '[') return false;
        i++;
        int nameStart = i;
        while (i < line.Length && (char.IsLetterOrDigit(line[i]) || line[i] == '_')) i++;
        if (i == nameStart) return false;              // etiket adı yok
        // Adın ardından boşluk ve tırnak gelmeli.
        while (i < line.Length && char.IsWhiteSpace(line[i])) i++;
        return i < line.Length && line[i] == '"';
    }
}
