using Serilog;
using System.Text.Json;
using System.Text.Json.Nodes;
using MediaFixer.Model;
using MediaFixer.Processors;

namespace MediaFixer;

/// <summary>
/// This program is intended to tag my downloaded files from GooglePhotos Takeout.
/// For some reason google takeout does not store the tags in the file, but I want to keep the info.
/// </summary>
internal class MediaFixer
{
    
    private readonly Options _options;
    private readonly List<string> _unusedProperties = new List<string>();
    private readonly RunMetrics _metrics = new RunMetrics();
    private readonly IReadOnlyCollection<MediaProcessor> _processors;

    private MediaFixer(Dictionary<string, string?> arguments)
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.ColoredConsole()
            .CreateLogger();

        _options = CreateOptions(arguments);

        SetUpDirectories();

        _processors = new MediaProcessor[] { 
            new ImageProcessor(_options), 
            new VideoProcessor(_options) };
    }

    internal static async Task FixMedia(Dictionary<string, string?> arguments)
    {
        if (arguments == null) throw new ArgumentNullException(nameof(arguments));

        var fixer = new MediaFixer(arguments);
        await fixer.Start();
    }

    private async Task Start()
    {
        Log.Information("======================= Starting tagging =======================");
        var files = GetFilesInDirectory(_options.Source);

        var limit = 1000;

        foreach (var f in files.media)
        {
            if (_metrics.FilesChecked >= limit)
            {
                Log.Error($"Limit of {limit} files reached");
                break;
            }
            await ProcessFile(f, files.all, _options.ScanMetaOnly);
            _metrics.FilesChecked++;
        }

        if (_options.ScanMetaOnly)
        {
            foreach (var prop in _unusedProperties)
            {
                Log.Warning($"Property {prop} is not being used");
            }
        }

        _metrics.Print();
        
        Log.Information("======================= Tagging completed =======================");
    }

    private Options CreateOptions(Dictionary<string, string?> arguments)
    {
        var args = string.Join(" ", arguments.Values);
        Log.Information($"Parsing arguments {args}");

        var config = new Options()
        {
            ConfigFile = GetConfigValue("config", arguments),
            Source = GetConfigValue("source", arguments),
            Destination = GetConfigValue("destination", arguments),
            TempDirectory = GetConfigValue("temp", arguments),
            ScanMetaOnly = HasConfigValue("scanMeta", arguments),
            OverWriteDestination = HasConfigValue("overWriteDestination", arguments),            
            ArchiveDirectory = GetConfigValue("archive", arguments)
        };

        if (!args.Any())
        {
            // try to load config from local file
            config = new Options()
            {
                ConfigFile = "config.json"
            };
        }

        if (config.ConfigFile != null && File.Exists(config.ConfigFile))
        {
            Log.Information($"Using config file at {config.ConfigFile}");
            config = ReadFileData<Options>(config.ConfigFile).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        config.Print();

        return config;
    }

    private void SetUpDirectories()
    {
        if (!Directory.Exists(_options.ArchiveDirectory))
        {
            Directory.CreateDirectory(_options.ArchiveDirectory);   
        }

        if (!Directory.Exists($"{_options.ArchiveDirectory}\\Videos"))
        {
            Directory.CreateDirectory($"{_options.ArchiveDirectory}\\Videos");
        }

        if (!Directory.Exists($"{_options.ArchiveDirectory}\\Metadata"))
        {
            Directory.CreateDirectory($"{_options.ArchiveDirectory}\\Metadata");
        }

        if (!Directory.Exists($"{_options.ArchiveDirectory}\\Images"))
        {
            Directory.CreateDirectory($"{_options.ArchiveDirectory}\\Images");
        }

        if (!Directory.Exists(_options.TempDirectory))
        {
            Directory.CreateDirectory(_options.TempDirectory);
        }

        if (!Directory.Exists(_options.Destination))
        {
            Directory.CreateDirectory(_options.Destination);
        }
    }

    private string GetConfigValue(string key, Dictionary<string, string?> arguments)
    {
        if (!arguments.TryGetValue(key.ToLower(), out var configValue) && configValue != null)
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
        var extensions = _processors.SelectMany(p => p.Extensions.Select(e => $"*{e}"));
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

    private async Task GetMetaAndProcessMedia(FileInfo file, FileInfo? metaFile)
    {
        var meta = metaFile == null ? null : await GetMetaData(metaFile.FullName);

        foreach (var p in _processors)
        {
            if (p.Extensions.Contains(file.Extension.ToLower()))
            {
                var result = p.Process(file, meta);
                _metrics.ImagesTagged += p.MediaType == MediaType.Image ? result.tagged : 0;
                _metrics.ImagesMoved += p.MediaType == MediaType.Image ? result.moved : 0;
                _metrics.ImagesConverted += p.MediaType == MediaType.Image ? result.convered : 0;
                _metrics.VideosMoved += p.MediaType == MediaType.Video ? result.moved : 0;
                _metrics.FilesSkipped += result.skipped;
                _metrics.Errors += result.errors;
                break;
            }
        }
    }

    /// <summary>
    /// I want to build a model for the google json,
    /// but don't want to check each file to get all the properties.
    /// With this function I can compare my model to the files to see if I am missing anything
    /// </summary>
    private async Task GetMetaAndScanIt(FileInfo? metaFile, string mediaFile)
    {
        if (metaFile == null)
        {
            _metrics.FilesSkipped++;
            Log.Error($"Could not find meta data for file {mediaFile}");
            return;
        }

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

        if (parsedMeta.GeoDataExif.Longitude > 0)
        {
            Log.Information($"{metaFile.FullName} has GPS data!");
        }
    }

    private async Task<GoogleMeta> GetMetaData(string file)
    {
        var gm = await ReadFileData<GoogleMeta>(file);
        gm.FilePath = file;
        return gm;
    }

    private async Task<T> ReadFileData<T>(string file)
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
        return ReadFileData<JsonObject>(file);
    }

    private bool HasConfigValue(string key, Dictionary<string, string?> arguments)
    {
        return arguments.ContainsKey(key.ToLower());
    }

    /// <summary>
    /// Get the meta json file and set the tags on the file
    /// </summary>
    private Task ProcessFile(FileInfo file, IReadOnlyCollection<FileInfo> allFiles, bool scanMetaOnly)
    {
        var scanMetaOnlyLog = scanMetaOnly ? " - Only scanning meta files" : string.Empty;
        Log.Information($"Processing {file.Name}{scanMetaOnlyLog}");

        var metaFile = allFiles.FirstOrDefault(x => x.Name == $"{file.Name}.json");

        return scanMetaOnly
            ? GetMetaAndScanIt(metaFile, file.Name)
            : GetMetaAndProcessMedia(file, metaFile);
    }
}