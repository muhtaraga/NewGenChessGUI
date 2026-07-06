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
        bool inMoves = false;   // etiketlerden sonra hamle bölümüne geçtik mi
        bool hasContent = false;

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            bool isTag = IsTagLine(line);

            // Hamle metninden sonra yeni bir etiket satırı: önceki oyun burada biter.
            if (isTag && inMoves)
            {
                yield return buffer.ToString();
                buffer.Clear();
                inMoves = false;
                hasContent = false;
            }

            if (!isTag && line.Trim().Length > 0)
                inMoves = true;

            if (isTag || line.Trim().Length > 0 || hasContent)
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
