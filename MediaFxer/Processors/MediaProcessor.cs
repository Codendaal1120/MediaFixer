using MediaFixer.Model;

namespace MediaFixer.Processors;

/// <summary>
/// Images needs to be tagged, converted (HEIC is not supported by iCloud) 
/// moved to the uplload and archived for NAS storage
/// </summary>
internal abstract class MediaProcessor
{
    public abstract IReadOnlyCollection<string> Extensions { get; }
    public abstract MediaType MediaType { get; }

    /// <summary>
    /// 1. Check destination of already exists
    /// 2. Create a copy (we will edit the copy)
    /// 3. Edit tags
    /// </summary>
    public abstract (int skipped, int errors, int tagged, int moved, int convered) Process(FileInfo sourceFile, GoogleMeta? meta);

    protected readonly Options Options;

    protected MediaProcessor(Options options)
    {
        Options = options;
    }
}
