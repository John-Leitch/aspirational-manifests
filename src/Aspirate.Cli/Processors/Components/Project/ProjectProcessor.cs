using AspireProject = Aspirate.Contracts.Models.AspireManifests.Components.V0.Project;

namespace Aspirate.Cli.Processors.Components.Project;

/// <summary>
/// A project component for version 0 of Aspire.
/// </summary>
public class ProjectProcessor(
    IFileSystem fileSystem,
    IContainerCompositionService containerCompositionService,
    IContainerDetailsService containerDetailsService)
        : BaseProcessor<ProjectTemplateData>(fileSystem)
{
    /// <inheritdoc />
    public override string ResourceType => AspireLiterals.Project;

    private readonly IReadOnlyCollection<string> _manifests =
    [
        $"{TemplateLiterals.DeploymentType}.yml",
        $"{TemplateLiterals.ServiceType}.yml",
    ];

    private readonly Dictionary<string, ContainerDetails> _containerDetailsCache = [];

    /// <inheritdoc />
    public override Resource? Deserialize(ref Utf8JsonReader reader) =>
        JsonSerializer.Deserialize<AspireProject>(ref reader);

    public override Task<bool> CreateManifests(KeyValuePair<string, Resource> resource, string outputPath)
    {
        var resourceOutputPath = Path.Combine(outputPath, resource.Key);

        EnsureOutputDirectoryExistsAndIsClean(resourceOutputPath);

        var project = resource.Value as AspireProject;

        if (!_containerDetailsCache.TryGetValue(resource.Key, out var containerDetails))
        {
            throw new InvalidOperationException($"Container details for project {resource.Key} not found.");
        }

        var data = new ProjectTemplateData(
            resource.Key,
            containerDetails.FullContainerImage,
            project.Env,
            _manifests);

        CreateDeployment(resourceOutputPath, data);
        CreateService(resourceOutputPath, data);
        CreateComponentKustomizeManifest(resourceOutputPath, data);

        LogCompletion(resourceOutputPath);

        return Task.FromResult(true);
    }

    public async Task BuildAndPushProjectContainer(KeyValuePair<string, Resource> resource)
    {
        var project = resource.Value as AspireProject;

        await containerCompositionService.BuildAndPushContainerForProject(project);

        AnsiConsole.MarkupLine($"\t[green]({EmojiLiterals.CheckMark}) Done: [/] Building and Pushing container for project [blue]{resource.Key}[/]");
    }

    public async Task PopulateContainerDetailsCacheForProject(KeyValuePair<string, Resource> resource)
    {
        var project = resource.Value as AspireProject;

        var details = await containerDetailsService.GetContainerDetails(resource.Key, project);

        var success = _containerDetailsCache.TryAdd(resource.Key, details);

        if (!success)
        {
            throw new InvalidOperationException($"Failed to add container details for project {resource.Key} to cache.");
        }

        AnsiConsole.MarkupLine($"\t[green]({EmojiLiterals.CheckMark}) Done: [/] Populated container details cache for project [blue]{resource.Key}[/]");
    }
}


