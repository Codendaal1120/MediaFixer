using MediaFixer.Model;
using Serilog;

namespace MediaFixer.Processors;

internal class MediaProcessResult
{
    public bool Skipped { get; set; }
    public bool Error { get; set; }
    public bool Moved { get; set; }
    public int Tagged { get; set; }
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
    public ILogger Logger { get; protected set; }

    protected MediaProcessor(Options options, ILogger logger)
    {
        Options = options;
        Logger = logger;
    }

    /// <summary>
    /// 1. Check destination of already exists
    /// 2. Create a copy (we will edit the copy)
    /// 3. Edit tags
    /// </summary>
    public abstract Task<MediaProcessResult> Process(FileInfo sourceFile, IReadOnlyCollection<FileInfo> allFiles);

    public abstract Task Initilize();

    protected readonly Options Options;    
}
