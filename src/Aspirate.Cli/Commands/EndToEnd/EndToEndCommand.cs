namespace Aspirate.Cli.Commands.EndToEnd;

/// <summary>
/// The command to convert Aspire Manifests to Kustomize Manifests.
/// </summary>
public partial class EndToEndCommand(
    IManifestFileParserService manifestFileParserService,
    IServiceProvider serviceProvider) : AsyncCommand<EndToEndInput>
{
    public const string EndToEndCommandName = "endtoend";
    public const string EndToEndDescription = "Builds, pushes containers, generates aspire manifest and kustomize manifests.";
    private static bool IsDatabase(Resource resource) =>
        resource is PostgresDatabase;

    public override async Task<int> ExecuteAsync(CommandContext context, EndToEndInput settings)
    {
        var appManifestFilePath = await GenerateAspireManifest(settings.PathToAspireProjectFlag);
        var aspireManifest = manifestFileParserService.LoadAndParseAspireManifest(appManifestFilePath);
        var finalManifests = new Dictionary<string, Resource>();

        var componentsToProcess = SelectManifestItemsToProcess(aspireManifest.Keys.ToList());

        var projectsToProcess = aspireManifest.Where(x => x.Value is Project && componentsToProcess.Contains(x.Key)).ToList();

        var projectProcessor = serviceProvider.GetRequiredKeyedService<IProcessor>(AspireLiterals.Project) as ProjectProcessor;

        await PopulateProjectContainerDetailsCache(projectsToProcess, projectProcessor);

        await BuildAndPushProjectContainers(projectsToProcess, projectProcessor);

        await GenerateManifests(settings, aspireManifest, componentsToProcess, finalManifests);

        LogCommandCompleted();

        return 0;
    }

    private async Task<string> GenerateAspireManifest(
        string appHostPath)
    {
        LogGeneratingAspireManifest();

        var compositionService = serviceProvider.GetRequiredService<IAspireManifestCompositionService>();

        var result = await compositionService.BuildManifestForProject(appHostPath);

        if (result.Success)
        {
            await LogCreatedManifestAtPath(result.FullPath);
            return result.FullPath;
        }

        AnsiConsole.MarkupLine($"[red]Failed to generate Aspire Manifest at: {result.FullPath}[/]");
        throw new InvalidOperationException("Failed to generate Aspire Manifest.");
    }

    private async Task PopulateProjectContainerDetailsCache(IReadOnlyCollection<KeyValuePair<string, Resource>> projectsToProcess, ProjectProcessor? projectProcessor)
    {
        LogGatheringContainerDetailsFromProjects();

        foreach (var resource in projectsToProcess)
        {
            await projectProcessor.PopulateContainerDetailsCacheForProject(resource);
        }

        await LogGatheringContainerDetailsFromProjectsCompleted();
    }

    private static async Task BuildAndPushProjectContainers(
        IReadOnlyCollection<KeyValuePair<string, Resource>> projectsToProcess,
        ProjectProcessor? projectProcessor)
    {
        LogBuildingAndPushingContainers();

        foreach (var resource in projectsToProcess)
        {
            await projectProcessor.BuildAndPushProjectContainer(resource);
        }

        await LogContainerCompositionCompleted();
    }

    private async Task GenerateManifests(EndToEndInput settings,
        Dictionary<string, Resource> aspireManifest,
        ICollection<string> componentsToProcess,
        Dictionary<string, Resource> finalManifests)
    {
        LogGeneratingManifests();

        foreach (var resource in aspireManifest.Where(x => x.Value is not UnsupportedResource && componentsToProcess.Contains(x.Key)))
        {
            await ProcessIndividualResourceManifests(settings, resource, finalManifests);
        }

        var finalHandler = serviceProvider.GetRequiredKeyedService<IProcessor>(AspireLiterals.Final);
        finalHandler.CreateFinalManifest(finalManifests, settings.OutputPathFlag);
    }

    private async Task ProcessIndividualResourceManifests(
        EndToEndInput input,
        KeyValuePair<string, Resource> resource,
        Dictionary<string, Resource> finalManifests)
    {
        if (resource.Value.Type is null)
        {
            LogTypeUnknown(resource.Key);
            return;
        }

        var handler = serviceProvider.GetKeyedService<IProcessor>(resource.Value.Type);

        if (handler is null)
        {
            LogUnsupportedType(resource.Key);
            return;
        }

        var success = await handler.CreateManifests(resource, input.OutputPathFlag);

        if (success && !IsDatabase(resource.Value))
        {
            finalManifests.Add(resource.Key, resource.Value);
        }
    }

    private static List<string> SelectManifestItemsToProcess(IEnumerable<string> manifestItems) =>
        AnsiConsole.Prompt(
            new MultiSelectionPrompt<string>()
                .Title("Select [green]components[/] to process from the loaded file")
                .PageSize(10)
                .Required()
                .MoreChoicesText("[grey](Move up and down to reveal more components)[/]")
                .InstructionsText(
                    "[grey](Press [blue]<space>[/] to toggle a component, " +
                    "[green]<enter>[/] to accept)[/]")
                .AddChoiceGroup("All Components", manifestItems));

}
