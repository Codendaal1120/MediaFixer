using Serilog;

namespace MediaFixer.Model;

internal class ProcessorOption
{
    /// <summary>
    /// Name of the processor class
    /// </summary>
    public string Name { get; init; } = null!;

    /// <summary>
    /// Supported extensions
    /// </summary>
    public IReadOnlyCollection<string> Extensions { get; init; } = null!;

    /// <summary>
    /// KvP of configuration
    /// </summary>
    public Dictionary<string, string> Options { get; init; } = null!;
}

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
    /// set with -overWriteDestination
    /// Specify what to do if the destination file exists (overwrite or ignore)
    /// </summary>
    public bool OverWriteDestination { get; init; }

    /// <summary>
    /// Specify weather to move the original media file
    /// </summary>
    public bool ArchiveMedia { get; init; }

    /// <summary>
    /// Image processors
    /// </summary>
    public IReadOnlyCollection<ProcessorOption> ImageProcessors { get; init; } = null!;

    /// <summary>
    /// Video processors
    /// </summary>
    public IReadOnlyCollection<ProcessorOption> VideoProcessors { get; init; } = null!;

    internal void Print()
    {
        Log.Information($"Config:");
        Log.Information($"\tSource: {Source}");
        Log.Information($"\tDestination: {Destination}");
        Log.Information($"\tArchiveDirectory: {ArchiveDirectory}");
        Log.Information($"\tTempDirectory: {TempDirectory}");
        Log.Information($"\tOverWriteDestination: {OverWriteDestination}");
    }
}


