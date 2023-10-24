using MediaFixer.Model;
using Serilog.Core;
using Serilog;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace MediaFixer.Processors;

internal abstract class GoogleProcessor : MediaProcessor
{
    protected GoogleProcessor(Options globalOptions, string processorName, ILogger logger) : base(globalOptions, processorName, logger)
    {        
        if (!ProcessorOptions.Options.TryGetValue("ArchiveMeta", out var configArchive) || !bool.TryParse(configArchive, out var archive))
        {
            throw new ArgumentException($"Missing or invalid config for {nameof(ArchiveMeta)}");
        }
        ArchiveMeta = archive;

        if (!ProcessorOptions.Options.TryGetValue("CheckAltMetaName", out var configCheckAltMetaName) || !bool.TryParse(configCheckAltMetaName, out var checkAltMetaName))
        {
            throw new ArgumentException($"Missing or invalid config for {nameof(CheckAltMetaName)}");
        }
        CheckAltMetaName = checkAltMetaName;

        if (!ProcessorOptions.Options.TryGetValue("ScanMetaOnly", out var configScanMetaOnly) || !bool.TryParse(configScanMetaOnly, out var scanMetaOnly))
        {
            throw new ArgumentException($"Missing or invalid config for {nameof(ScanMetaOnly)}");
        }
        ScanMetaOnly = scanMetaOnly;

        Extensions = ProcessorOptions.Extensions;
    }

    /// <summary>
    /// Specify weather to move the original meta file
    /// </summary>
    protected bool ArchiveMeta { get; init; }

    /// <summary>
    /// If metadata for file IMG_10(1).JPG cannot be found, check IMG_10.JPG(1)
    /// </summary>
    protected bool CheckAltMetaName { get; init; }

    /// <summary>
    /// set with -scanMeta 
    /// Enable this to check 
    /// </summary>
    protected bool ScanMetaOnly { get; init; }
    
    private readonly List<string> _unusedProperties = new List<string>();

    public override Task Initilize()
    {
        HasInitilized = true;
        return Task.CompletedTask;
    }

    protected async Task<GoogleMeta> GetMetaData(FileInfo metaFile)
    {
        var gm = await JsonReader.ReadFileData<GoogleMeta>(metaFile!.FullName);
        gm.FilePath = metaFile.FullName;
        return gm;
    }

    protected FileInfo? GetMetaFile(FileInfo sourceFile, IReadOnlyCollection<FileInfo> allFiles)
    {
        var metaFile = allFiles.FirstOrDefault(x => x.Name == $"{sourceFile.Name}.json");
        if (metaFile == null && CheckAltMetaName)
        {
            var num = Regex.Match(sourceFile.Name, "\\((\\d+)\\)");
            if (num.Success && num.Groups[1] != num)
            {
                var altName = $"{sourceFile.Name.Replace($"({num.Groups[1]})", "")}({num.Groups[1]}).json";
                metaFile = allFiles.FirstOrDefault(x => x.Name == altName);
            }
        }

        return metaFile;
    }

    protected Task<JsonObject> GetMetaDataObject(string file)
    {
        return JsonReader.ReadFileData<JsonObject>(file);
    }

    /// <summary>
    /// I want to build a model for the google json,
    /// but don't want to check each file to get all the properties.
    /// With this function I can compare my model to the files to see if I am missing anything
    /// </summary>
    protected async Task<MediaProcessResult> GetMetaAndScanIt(FileInfo? metaFile, string mediaFile)
    {
        if (metaFile == null)
        {
            //_metrics.FilesSkipped++;
            Log.Error($"Could not find meta data for file {mediaFile}");
            return new MediaProcessResult() { Error = true };
        }

        var dMeta = await GetMetaDataObject(metaFile.FullName);
        var dynamicProps = GetJsonObjectProperties(dMeta);

        // if its stupid and it works, its not stupid
        var parsedMeta = await GetMetaData(metaFile);
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

        return new MediaProcessResult();
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
}
