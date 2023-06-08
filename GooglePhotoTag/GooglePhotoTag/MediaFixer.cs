using Serilog;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MediaFixer;

/// <summary>
/// This program is intended to tag my downloaded files from GooglePhotos Takeout.
/// For some reason google takeout does not store the tags in the file, but I want to keep the info.
/// </summary>
internal partial class MediaFixer
{
    private readonly IReadOnlyCollection<string> _imageExtensions = new[] { ".jpg", ".png" };

    private readonly Dictionary<string, string?> _options;

    private readonly List<string> _unusedProperties = new List<string>();

    private readonly IReadOnlyCollection<string> _videoExtensions = new[] { ".mp4" };

    private MediaFixer(Dictionary<string, string?> options)
    {
        _options = options;
        Log.Logger = new LoggerConfiguration()
            .WriteTo.ColoredConsole()
            .CreateLogger();
    }

    internal static async Task FixMedia(Dictionary<string, string?> options)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        var fixer = new MediaFixer(options);
        await fixer.Start();
    }
    private string GetConfigValue(string key)
    {
        if (!_options.TryGetValue(key.ToLower(), out var configValue) && configValue != null)
        {
            throw new InvalidOperationException($"Could not find config value for {key}");
        }

        return configValue!;
    }

    private (IReadOnlyCollection<FileInfo> all, IReadOnlyCollection<FileInfo> media) GetFilesInDirectory(string directory)
    {
        var dir = new DirectoryInfo(directory);
        if (!dir.Exists)
        {
            return (Array.Empty<FileInfo>(), Array.Empty<FileInfo>());
        }

        Log.Information($"Scanning directory {directory}");

        var allFiles = dir.GetFiles().ToList();
        var extensions = _videoExtensions.Select(x => $"*{x}").Concat(_imageExtensions.Select(y => $"*{y}"));
        var mediaFiles = extensions.SelectMany(ext => dir.GetFiles(ext).Select(x => x)).ToList();

        foreach (var subDir in dir.GetDirectories())
        {
            var subDirFiles = GetFilesInDirectory(subDir.FullName);
            allFiles.AddRange(subDirFiles.all);
            mediaFiles.AddRange(subDirFiles.media);
        }

        return (allFiles, mediaFiles);
    }

    private IReadOnlyCollection<string> GetJsonNodeProperties(string parent, JsonNode node)
    {
        var props = new List<string>();

        if (node is JsonObject nodeObject)
        {
            foreach (var kvp in nodeObject)
            {
                props.Add($"{parent}.{kvp.Key}");
                props.AddRange(GetJsonNodeProperties($"{parent}.{kvp.Key}", kvp.Value!));
            }
        }

        if (node is JsonArray array)
        {
            foreach (var element in array)
            {
                props.AddRange(GetJsonNodeProperties(parent, element!));
            }
        }

        return props;
    }

    private IReadOnlyCollection<string> GetJsonObjectProperties(JsonObject dyn)
    {
        var props = new List<string>();

        foreach (var kvp in dyn)
        {
            props.Add(kvp.Key);
            props.AddRange(GetJsonNodeProperties(kvp.Key, kvp.Value!));
        }

        return props;
    }

    private async Task GetMetaAndProcessImage(FileInfo file, FileInfo metaFile)
    {
        var meta = await GetMetaData(metaFile.FullName);

        if (_imageExtensions.Contains(file.Extension))
        {
            ProcessImage(file, meta);
        }

        if (_videoExtensions.Contains(file.Extension))
        {
            ProcessImage(file, meta);
        }
    }

    /// <summary>
    /// I want to build a model for the google json,
    /// but don't want to check each file to get all the properties.
    /// With this function I can compare my model to the files to see if I am missing anything
    /// </summary>
    private async Task GetMetaAndScanIt(FileInfo metaFile)
    {
        var dMeta = await GetMetaDataObject(metaFile.FullName);
        var dynamicProps = GetJsonObjectProperties(dMeta);

        // if its stupid and it works, its not stupid
        var parsedMeta = await GetMetaData(metaFile.FullName);
        var parsedJson = JsonSerializer.Serialize(parsedMeta);
        var parsedJsonObject = JsonSerializer.Deserialize<JsonObject>(parsedJson);
        var parsedProps = GetJsonObjectProperties(parsedJsonObject!);

        foreach (var prop in dynamicProps)
        {
            if (parsedProps.All(x => !string.Equals(x, prop, StringComparison.CurrentCultureIgnoreCase)))
            {
                if (!_unusedProperties.Contains(prop))
                {
                    _unusedProperties.Add(prop);
                }
            }
        }
    }

    private Task<GoogleMeta> GetMetaData(string file)
    {
        return GetMetaData<GoogleMeta>(file);
    }

    private async Task<T> GetMetaData<T>(string file)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        using var sr = new StreamReader(file);
        var content = await sr.ReadToEndAsync();
        return JsonSerializer.Deserialize<T>(content, options)!;
    }

    private Task<JsonObject> GetMetaDataObject(string file)
    {
        return GetMetaData<JsonObject>(file);
    }

    private string GetPropertyValue(string key, dynamic json)
    {
        return json.GetType().GetProperty(key).GetValue(json, null).ToString();
    }

    private bool HasConfigValue(string key)
    {
        return _options.ContainsKey(key.ToLower());
    }

    /// <summary>
    /// Get the meta json file and set the tags on the file
    /// </summary>
    private Task ProcessFile(FileInfo file, IReadOnlyCollection<FileInfo> allFiles, bool scanMetaOnly)
    {
        var scanMetaOnlyLog = scanMetaOnly ? " - Only scanning meta files" : string.Empty;
        Log.Information($"Processing {file.Name}{scanMetaOnlyLog}");

        var metaFile = allFiles.FirstOrDefault(x => x.Name == $"{file.Name}.json");
        if (metaFile == null)
        {
            Log.Error($"Could not find meta data for file {file.Name}");
            return Task.CompletedTask;
        }

        return scanMetaOnly
            ? GetMetaAndScanIt(metaFile)
            : GetMetaAndProcessImage(file, metaFile);
    }

    private void ProcessImage(FileInfo file, dynamic meta)
    {
        var tfile = TagLib.File.Create(file.FullName);
        var tag = tfile.Tag as TagLib.Image.CombinedImageTag;

        //if (tag.DateTime

        //string title = tfile.Tag.Title;
        //var tag = tfile.Tag as TagLib.Image.CombinedImageTag;
        //DateTime? snapshot = tag.DateTime;
        //Console.WriteLine("Title: {0}, snapshot taken on {1}", title, snapshot);

        //// change title in the file
        //tfile.Tag.Title = "my new title";
        //tfile.Save();
    }

    private async Task Start()
    {
        var source = GetConfigValue("source");
        var files = GetFilesInDirectory(source);
        var scanMetaOnly = HasConfigValue("scanMeta");

        foreach (var f in files.media)
        {
            await ProcessFile(f, files.all, scanMetaOnly);
        }

        if (scanMetaOnly)
        {
            foreach (var prop in _unusedProperties)
            {
                Log.Error($"Property {prop} is not being used");
            }
        }
    }
}