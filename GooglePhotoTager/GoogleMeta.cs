namespace MediaFixer;

internal class GoogleMeta
{
    public string FilePath { get; set; } = null!;    
    public string Description { get; init; } = null!;
    public string ImageViews { get; init; } = null!;
    public string Title { get; init; } = null!;
    public string Url { get; init; } = null!;

    public GoogleMetaCreationTime CreationTime { get; init; } = null!;
    public GoogleMetaGeoData GeoData { get; init; } = null!;
    public GoogleMetaGeoData GeoDataExif { get; init; } = null!;
    public GoogleMetaPhotosOrigin GooglePhotosOrigin { get; init; } = null!;
    public GoogleMetaCreationTime PhotoTakenTime { get; init; } = null!;

    public IReadOnlyCollection<GoogleMetaPerson> People { get; init; } = null!;        
}