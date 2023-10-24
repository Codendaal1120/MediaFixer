using MediaFixer.Model;
using Serilog;
using System.Drawing.Imaging;
using System.Text.Json.Nodes;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace MediaFixer.Processors;

internal abstract class GoogleProcessor : MediaProcessor
{
    protected GoogleProcessor(Options options) : base(options)
    {
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

    protected async Task<GoogleMeta> GetMetaData(FileInfo sourceFile, IReadOnlyCollection<FileInfo> allFiles)
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

        var gm = await JsonReader.ReadFileData<GoogleMeta>(metaFile!.FullName);
        gm.FilePath = metaFile.FullName;
        return gm;
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
    protected Task GetMetaAndScanIt(FileInfo? metaFile, string mediaFile)
    {
        throw new NotImplementedException();

        //if (metaFile == null)
        //{
        //    //_metrics.FilesSkipped++;
        //    Log.Error($"Could not find meta data for file {mediaFile}");
        //    return;
        //}

        //var dMeta = await GetMetaDataObject(metaFile.FullName);
        //var dynamicProps = GetJsonObjectProperties(dMeta);

        //// if its stupid and it works, its not stupid
        //var parsedMeta = await GetMetaData(metaFile.FullName);
        //var parsedJson = JsonSerializer.Serialize(parsedMeta);
        //var parsedJsonObject = JsonSerializer.Deserialize<JsonObject>(parsedJson);
        //var parsedProps = GetJsonObjectProperties(parsedJsonObject!);

        //foreach (var prop in dynamicProps)
        //{
        //    if (parsedProps.All(x => !string.Equals(x, prop, StringComparison.CurrentCultureIgnoreCase)))
        //    {
        //        if (!_unusedProperties.Contains(prop))
        //        {
        //            _unusedProperties.Add(prop);
        //        }
        //    }
        //}

        //if (parsedMeta.GeoDataExif.Longitude > 0)
        //{
        //    Log.Information($"{metaFile.FullName} has GPS data!");
        //}
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

/// <summary>
/// Videos does not move, only archive since these cannot be uploaded to iCloud.
/// </summary>
internal class GoogleVideoProcessor : GoogleProcessor
{
    public override MediaType MediaType => MediaType.Video;
    public override IReadOnlyCollection<string> Extensions => new[] { ".mp4", ".m4v" };

 

    public GoogleVideoProcessor(Options options) : base(options)
    {
        var processorConfig = options.VideoProcessors.FirstOrDefault(x => x.Name.Equals(nameof(GoogleVideoProcessor)));
        if (processorConfig == null)
        {
            throw new ArgumentNullException("GoogleVideoProcessorConfig");
        }
    }

    public override async Task<MediaProcessResult> Process(FileInfo sourceFile, IReadOnlyCollection<FileInfo> allFiles)
    {
        var meta = await GetMetaData(sourceFile, allFiles);

        if (!Options.ArchiveMedia)
        {
            return new MediaProcessResult { Skipped = true };
        }

        var videoArchiveFilePath = Path.Join(Options.ArchiveDirectory, "Videos", sourceFile.Name);
        var metaArchiveFilePath = Path.Join(Options.ArchiveDirectory, "Metadata", $"{sourceFile.Name}.json");

        // Archive file
        File.Move(sourceFile.FullName, videoArchiveFilePath);
        if (meta != null)
        {
            File.Move(meta.FilePath, metaArchiveFilePath);
        }

        return new MediaProcessResult { Moved = true };
    }

}
