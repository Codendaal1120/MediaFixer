namespace MediaFixer.Processors;

internal class MediaProcessResult
{
    public bool Skipped { get; set; }
    public bool Error { get; set; }
    public bool Moved { get; set; }
    public int Tagged { get; set; }
    public bool Converted { get; set; }
}
