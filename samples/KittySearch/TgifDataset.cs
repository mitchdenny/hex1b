sealed class TgifDataset
{
    private const string IndexFileName = "index.tsv";

    private readonly IReadOnlyList<TgifDatasetEntry> _entries;
    private readonly Dictionary<string, TgifResult> _resultCache = new(StringComparer.Ordinal);
    private readonly object _cacheGate = new();

    private TgifDataset(IReadOnlyList<TgifDatasetEntry> entries)
    {
        _entries = entries;
    }

    internal int Count => _entries.Count;

    internal static TgifDataset Open(string dataDirectory)
    {
        var fullDataDirectory = Path.GetFullPath(dataDirectory);
        var indexPath = Path.Combine(fullDataDirectory, IndexFileName);
        using var reader = File.OpenText(indexPath);

        var header = reader.ReadLine();
        if (!string.Equals(header, "entry\tdescription\tsource_url", StringComparison.Ordinal))
            throw new InvalidDataException("The TGIF index has an unsupported format.");

        var entries = new List<TgifDatasetEntry>();
        while (reader.ReadLine() is { } line)
        {
            var columns = line.Split('\t');
            if (columns.Length != 3)
                throw new InvalidDataException("The TGIF index contains a malformed row.");

            var imagePath = Path.GetFullPath(
                Path.Combine(fullDataDirectory, columns[0].Replace('/', Path.DirectorySeparatorChar)));
            if (!imagePath.StartsWith(
                    fullDataDirectory + Path.DirectorySeparatorChar,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException("The TGIF index contains an invalid image path.");
            }

            if (!File.Exists(imagePath))
                throw new InvalidDataException($"The TGIF image does not exist: {columns[0]}.");

            entries.Add(new TgifDatasetEntry(imagePath, columns[1], columns[2]));
        }

        return new TgifDataset(entries);
    }

    internal IReadOnlyList<TgifResult> Search(string query, int limit)
    {
        var terms = query.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (terms.Length == 0)
            return [];

        return _entries
            .Where(entry => terms.All(term =>
                entry.Description.Contains(term, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(entry => SearchRank(entry.Description, query))
            .ThenBy(entry => entry.Description, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .Select(LoadResult)
            .ToArray();
    }

    private TgifResult LoadResult(TgifDatasetEntry entry)
    {
        lock (_cacheGate)
        {
            if (_resultCache.TryGetValue(entry.ImagePath, out var cached))
                return cached;
        }

        var encoded = File.ReadAllBytes(entry.ImagePath);
        var preview = GifDecoder.DecodeFirstFrame(encoded)
            ?? throw new InvalidDataException(
                $"The TGIF image {entry.ImagePath} could not be decoded.");
        var result = new TgifResult(
            entry.Description,
            entry.SourceUrl,
            encoded,
            preview.Data,
            preview.Width,
            preview.Height);

        lock (_cacheGate)
        {
            if (_resultCache.TryGetValue(entry.ImagePath, out var cached))
                return cached;
            _resultCache.Add(entry.ImagePath, result);
            return result;
        }
    }

    private static int SearchRank(string description, string query)
    {
        if (description.Equals(query, StringComparison.OrdinalIgnoreCase))
            return 0;
        if (description.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            return 1;
        return 2;
    }
}

sealed record TgifDatasetEntry(
    string ImagePath,
    string Description,
    string SourceUrl);
