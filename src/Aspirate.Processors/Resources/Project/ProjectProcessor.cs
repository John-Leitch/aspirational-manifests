namespace Aspirate.Processors.Resources.Project;

/// <summary>
/// A project component for version 0 of Aspire.
/// </summary>
public sealed class ProjectProcessor(
    IFileSystem fileSystem,
    IAnsiConsole console,
    ISecretProvider secretProvider,
    IContainerCompositionService containerCompositionService,
    IContainerDetailsService containerDetailsService,
    IManifestWriter manifestWriter)
    : BaseProjectProcessor(
        fileSystem,
        console,
        secretProvider,
        containerCompositionService,
        containerDetailsService,
        manifestWriter)
{
    /// <inheritdoc />
    public override string ResourceType => AspireComponentLiterals.Project;
}
