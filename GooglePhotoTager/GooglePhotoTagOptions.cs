namespace MediaFixer;

internal class GooglePhotoTagOptions
{
    /// <summary>
    /// set with -source <PATH>
    /// The source of the media and meta files
    /// </summary>
    public string Source {  get; init; } = null!;

    /// <summary>
    /// set with -destination <PATH>
    /// The destination to move the tagged media to
    /// </summary>
    public string Destination { get; init; } = null!;

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
}


