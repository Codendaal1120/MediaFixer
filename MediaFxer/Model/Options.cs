using Serilog;

namespace MediaFixer.Model;

internal class Options
{
    /// <summary>
    /// set with -config <PATH>
    /// Specify a config file to load
    /// </summary>
    public string? ConfigFile { get; init; } = null!;

    /// <summary>
    /// set with -source <PATH>
    /// The source of the media and meta files
    /// </summary>
    public string Source { get; init; } = null!;

    /// <summary>
    /// set with -destination <PATH>
    /// The destination to move the tagged media to
    /// </summary>
    public string Destination { get; init; } = null!;

    /// <summary>
    /// set with -archive <PATH>
    /// The destination to move finished files to
    /// </summary>
    public string ArchiveDirectory { get; init; } = null!;

    /// <summary>
    /// set with -temp <PATH>
    /// The destination to move finished files to
    /// </summary>
    public string TempDirectory { get; init; } = null!;

    /// <summary>
    /// set with -scanMeta 
    /// Enable this to check 
    /// </summary>
    public bool ScanMetaOnly { get; init; }

    /// <summary>
    /// set with -overWriteDestination
    /// Specify what to do if the destination file exists (overwrite or ignore)
    /// </summary>
    public bool OverWriteDestination { get; init; }

    /// <summary>
    /// Specify weather to move the original media file
    /// </summary>
    public bool ArchiveMedia { get; init; }

    /// <summary>
    /// Specify weather to move the original meta file
    /// </summary>
    public bool ArchiveMeta { get; init; }

    /// <summary>
    /// If metadata for file IMG_10(1).JPG cannot be found, check IMG_10.JPG(1)
    /// </summary>
    public bool CheckAltMetaName { get; init; }
    

    internal void Print()
    {
        Log.Information($"Config:");
        Log.Information($"\tSource: {Source}");
        Log.Information($"\tDestination: {Destination}");
        Log.Information($"\tArchiveDirectory: {ArchiveDirectory}");
        Log.Information($"\tTempDirectory: {TempDirectory}");
        Log.Information($"\tScanMetaOnly: {ScanMetaOnly}");
        Log.Information($"\tOverWriteDestination: {OverWriteDestination}");
    }
}


