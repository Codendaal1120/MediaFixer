using MediaFixer.Model;

namespace MediaFixer.Processors;

internal class MediaProcessResult
{
    public bool Skipped { get; set; }
    public bool Error { get; set; }
    public bool Moved { get; set; }
    public bool Tagged { get; set; }
    public bool Converted { get; set; }
}

/// <summary>
/// Images needs to be tagged, converted (HEIC is not supported by iCloud) 
/// moved to the uplload and archived for NAS storage
/// </summary>
internal abstract class MediaProcessor
{
    public abstract IReadOnlyCollection<string> Extensions { get; }
    public abstract MediaType MediaType { get; }
    public bool HasInitilized { get; protected set; }

    /// <summary>
    /// 1. Check destination of already exists
    /// 2. Create a copy (we will edit the copy)
    /// 3. Edit tags
    /// </summary>
    public abstract Task<MediaProcessResult> Process(FileInfo sourceFile, IReadOnlyCollection<FileInfo> allFiles);

    public abstract Task Initilize();

    protected readonly Options Options;

    protected MediaProcessor(Options options)
    {
        Options = options;
    }

    
}
