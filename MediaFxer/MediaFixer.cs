using Serilog;
using System.Text.Json;
using System.Text.Json.Nodes;
using MediaFixer.Model;
using MediaFixer.Processors;
using System.Text.RegularExpressions;

namespace MediaFixer;

/// <summary>
/// This program is intended to tag my downloaded files from GooglePhotos Takeout.
/// For some reason google takeout does not store the tags in the file, but I want to keep the info.
/// </summary>
internal class MediaFixer
{    
    private readonly Options _options;
  
    private readonly RunMetrics _metrics = new RunMetrics();
    private readonly IReadOnlyCollection<MediaProcessor> _processors;

    private MediaFixer(Dictionary<string, string?> arguments)
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.ColoredConsole()
            .CreateLogger();

        _options = CreateOptions(arguments);

        SetUpDirectories();

        _processors = ProcessorFactory.GetProcessors(_options);
    }

    internal static async Task FixMedia(Dictionary<string, string?> arguments)
    {
        if (arguments == null) throw new ArgumentNullException(nameof(arguments));

        var fixer = new MediaFixer(arguments);
        await fixer.Start();
    }

    private async Task Start()
    {
        Log.Information("======================= Starting fixing =======================");
        var files = GetFilesInDirectory(_options.Source);

        var limit = 1000;

        foreach (var f in files.media)
        {
            if (_metrics.FilesChecked >= limit)
            {
                Log.Error($"Limit of {limit} files reached");
                break;
            }
            await ProcessFile(f, files.all);
            _metrics.FilesChecked++;
        }

        foreach (var p in _processors)
        {
            if (p.HasInitilized)
            {
                //finalize
            }
        }

        _metrics.Print();
        
        Log.Information("======================= Fixing completed =======================");
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
            config = JsonReader.ReadFileData<Options>(config.ConfigFile).ConfigureAwait(false).GetAwaiter().GetResult();
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

    private bool HasConfigValue(string key, Dictionary<string, string?> arguments)
    {
        return arguments.ContainsKey(key.ToLower());
    }

    /// <summary>
    /// Runs the file through the desired processor
    /// </summary>
    private async Task ProcessFile(FileInfo file, IReadOnlyCollection<FileInfo> allFiles)
    {
        Log.Information($"Processing {file.Name}");

        foreach (var p in _processors)
        {
            if (p.Extensions.Contains(file.Extension.ToLower()))
            {
                if (!p.HasInitilized) { await p.Initilize(); }
                var result = await p.Process(file, allFiles);
                _metrics.ImagesTagged += p.MediaType == MediaType.Image && result.Tagged ? 1 : 0;
                _metrics.ImagesMoved += p.MediaType == MediaType.Image && result.Moved ? 1 : 0;
                _metrics.ImagesConverted += p.MediaType == MediaType.Image && result.Converted ? 1 : 0;

                _metrics.VideosTagged += p.MediaType == MediaType.Video  && result.Tagged ? 1 : 0;
                _metrics.VideosConverted += p.MediaType == MediaType.Video  && result.Converted ? 1 : 0;
                _metrics.VideosMoved += p.MediaType == MediaType.Video  && result.Moved ? 1 : 0;

                _metrics.FilesSkipped += result.Skipped ? 1 : 0;
                _metrics.Errors += result.Error ? 1 : 0;
            }
        }
    }
}