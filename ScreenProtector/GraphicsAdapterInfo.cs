namespace ScreenProtector;

public sealed class GraphicsAdapterInfo
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public bool IsDefaultOutputAdapter { get; init; }

    public override string ToString() => Name;
}