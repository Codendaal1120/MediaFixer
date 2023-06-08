using ExifLibrary;
using Serilog;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using static System.Net.Mime.MediaTypeNames;
using Image = System.Drawing.Image;

namespace MediaFixer;

/// <summary>
/// This program is intended to tag my downloaded files from GooglePhotos Takeout.
/// For some reason google takeout does not store the tags in the file, but I want to keep the info.
/// </summary>
internal class GooglePhotoTager
{
    private readonly IReadOnlyCollection<string> _imageExtensions = new[] { ".jpg", ".png" };
    private readonly GooglePhotoTagOptions _options;
    private readonly List<string> _unusedProperties = new List<string>();
    private readonly IReadOnlyCollection<string> _videoExtensions = new[] { ".mp4" };

    private GooglePhotoTager(Dictionary<string, string?> arguments)
    {
        _options = CreateOptions(arguments);
        Log.Logger = new LoggerConfiguration()
            .WriteTo.ColoredConsole()
            .CreateLogger();
    }

    internal static async Task FixMedia(Dictionary<string, string?> arguments)
    {
        if (arguments == null) throw new ArgumentNullException(nameof(arguments));
       
        var fixer = new GooglePhotoTager(arguments);
        await fixer.Start();
    } 

    private async Task Start()
    {
        Log.Information("======================= Starting tagging =======================");
        var files = GetFilesInDirectory(_options.Source);

        var count = 0;
        var limit = 1000;

        foreach (var f in files.media)
        {
            if (count >= limit)
            {
                Log.Error($"Limit of {limit} files reached");
                break;
            }
            await ProcessFile(f, files.all, _options.ScanMetaOnly);
            count++;
        }

        if (_options.ScanMetaOnly)
        {
            foreach (var prop in _unusedProperties)
            {                
                Log.Error($"Property {prop} is not being used");                
            }
        }

        Log.Information("======================= Tagging completed =======================");
    }

    private GooglePhotoTagOptions CreateOptions(Dictionary<string, string?> arguments)
    {      
        return new GooglePhotoTagOptions()
        {
            Source = GetConfigValue("source", arguments),
            Destination = GetConfigValue("destination", arguments),
            ScanMetaOnly = HasConfigValue("scanMeta", arguments),
            OverWriteDestination = HasConfigValue("overWriteDestination", arguments),
            ArchiveDirectory = GetConfigValue("archive", arguments)
        };
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
            ProcessVideo(file, meta);
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

        if (parsedMeta.GeoDataExif.Longitude > 0)
        {
            Log.Information($"{metaFile.FullName} has GPS data!");
        }
    }

    private async Task<GoogleMeta> GetMetaData(string file)
    {
        var gm = await GetMetaData<GoogleMeta>(file);
        gm.FilePath = file;
        return gm;
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
        if (metaFile == null)
        {
            Log.Error($"Could not find meta data for file {file.Name}");
            return Task.CompletedTask;
        }

        return scanMetaOnly
            ? GetMetaAndScanIt(metaFile)
            : GetMetaAndProcessImage(file, metaFile);
    }

    private DateTime GetPropertyDateTaken(GoogleMeta meta, string file)
    {
        var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        if (meta.PhotoTakenTime.Timestamp != null)
        {
            var unixtime = Convert.ToInt64(meta.PhotoTakenTime.Timestamp);
            return epoch.AddSeconds(unixtime);
        }

        if (meta.CreationTime.Timestamp != null)
        {
            var unixtime = Convert.ToInt64(meta.CreationTime.Timestamp);

            return epoch.AddSeconds(unixtime);
        }

        Log.Error($"Could not determine date taken for {file}");
        return epoch;
    }

#pragma warning disable CA1416 // Validate platform compatibility
    /// <summary>
    /// 1. Check destination of already exists
    /// 2. Create a copy (we will edit the copy)
    /// 3. Edit tags
    /// </summary>
    private void ProcessImage(FileInfo sourceFile, GoogleMeta meta)
    {
        var destinationFilePath = Path.Join(_options.Destination, sourceFile.Name);
        var imageArchiveFilePath = Path.Join(_options.ArchiveDirectory, "Images", sourceFile.Name);
        var metaArchiveFilePath = Path.Join(_options.ArchiveDirectory, "Metadata", $"{sourceFile.Name}.json");

        if (File.Exists(destinationFilePath))
        {
            if (!_options.OverWriteDestination)
            {
                Log.Warning($"{destinationFilePath} already exists, ignoring");
                return;
            }
        }

        try
        {
            // Set the DateTaken using ExifLib
            var img = ImageFile.FromFile(sourceFile.FullName);
            img.Properties.Set(ExifTag.DateTimeDigitized, GetPropertyDateTaken(meta, sourceFile.FullName));
            img.Properties.Set(ExifTag.GPSDestLongitude, meta.GeoDataExif.Longitude);
            img.Properties.Set(ExifTag.GPSDestLatitude, meta.GeoDataExif.Latitude);
            img.Save(destinationFilePath);

            // Archive file
            File.Move(sourceFile.FullName, imageArchiveFilePath);
            File.Move(meta.FilePath, metaArchiveFilePath);
        }
        catch(Exception ex)
        {
            Log.Error(ex, $"Unable to process {sourceFile.FullName}");
        }
    }

    private void ProcessVideo(FileInfo sourceFile, GoogleMeta meta)
    {
        var videoArchiveFilePath = Path.Join(_options.ArchiveDirectory, "Videos", sourceFile.Name);
        var metaArchiveFilePath = Path.Join(_options.ArchiveDirectory, "Metadata", $"{sourceFile.Name}.json");

        // Archive file
        File.Move(sourceFile.FullName, videoArchiveFilePath);
        File.Move(meta.FilePath, metaArchiveFilePath);
    }
#pragma warning restore CA1416 // Validate platform compatibility



}