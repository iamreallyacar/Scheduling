using Microsoft.EntityFrameworkCore;
using TireScheduler.Api.Database;
using TireScheduler.Api.Errors;
using TireScheduler.Api.Modules.MasterData.Products;

namespace TireScheduler.Api.Modules.MasterData.Routings;

// F-MD-3: the routing of a product, read and replaced as a whole.
internal sealed class RoutingRegister(MasterDataDbContext context, RequestTransaction requestTransaction)
{
    private const int DefaultSetupMinutes = 0;
    private const int DefaultUnitsPerCycle = 1;
    private const int DefaultMinLagMinutes = 0;

    public async Task<RoutingResponse> GetAsync(int productId, CancellationToken cancellationToken)
    {
        if (!await context.Products.AnyAsync(product => product.Id == productId, cancellationToken))
        {
            throw ProductNotFound();
        }

        return await ReadAsync(productId, cancellationToken);
    }

    public async Task<RoutingResponse> ReplaceAsync(int productId, RoutingRequest request, CancellationToken cancellationToken)
    {
        IReadOnlyList<RoutingStageRequest> stages = request.Stages.Validated();
        IReadOnlyList<RoutingDependencyRequest> dependencies = request.Dependencies.Validated();

        await using (OpenTransaction transaction = await requestTransaction.BeginAsync(context, cancellationToken))
        {
            await LockProductAsync(productId, cancellationToken);
            await RejectInvalidStagesAsync(stages, cancellationToken);
            StageLink[] links = dependencies
                .Select(dependency => new StageLink(
                    dependency.PredecessorSequenceNo.Validated(),
                    dependency.SuccessorSequenceNo.Validated(),
                    dependency.MinLagMinutes ?? DefaultMinLagMinutes))
                .ToArray();
            RoutingRules.RejectInvalidDependencies(stages.Count, links);

            // The dependencies go with their stages (cascade).
            await context.RoutingStages.Where(stage => stage.ProductId == productId).ExecuteDeleteAsync(cancellationToken);
            Dictionary<int, RoutingStage> newStageBySequenceNo = stages
                .Select(stage => new RoutingStage
                {
                    ProductId = productId,
                    SequenceNo = stage.SequenceNo.Validated(),
                    ResourceTypeId = stage.ResourceTypeId.Validated(),
                    SetupMinutes = stage.SetupMinutes ?? DefaultSetupMinutes,
                    CycleMinutes = stage.CycleMinutes.Validated(),
                    UnitsPerCycle = stage.UnitsPerCycle ?? DefaultUnitsPerCycle,
                })
                .ToDictionary(stage => stage.SequenceNo);
            context.RoutingStages.AddRange(newStageBySequenceNo.Values);
            context.RoutingDependencies.AddRange(links.Select(link => new RoutingDependency
            {
                ProductId = productId,
                PredecessorStage = newStageBySequenceNo[link.PredecessorSequenceNo],
                SuccessorStage = newStageBySequenceNo[link.SuccessorSequenceNo],
                MinLagMinutes = link.MinLagMinutes,
            }));
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        return await ReadAsync(productId, cancellationToken);
    }

    private async Task<RoutingResponse> ReadAsync(int productId, CancellationToken cancellationToken)
    {
        List<RoutingStageResponse> stages = await context.RoutingStages
            .Where(stage => stage.ProductId == productId)
            .OrderBy(stage => stage.SequenceNo)
            .Select(stage => new RoutingStageResponse
            {
                SequenceNo = stage.SequenceNo,
                ResourceType = new NamedReference { Id = stage.ResourceType!.Id, Name = stage.ResourceType.Name },
                SetupMinutes = stage.SetupMinutes,
                CycleMinutes = stage.CycleMinutes,
                UnitsPerCycle = stage.UnitsPerCycle,
            })
            .ToListAsync(cancellationToken);
        List<RoutingDependencyResponse> dependencies = await context.RoutingDependencies
            .Where(dependency => dependency.ProductId == productId)
            .OrderBy(dependency => dependency.SuccessorStage!.SequenceNo)
            .ThenBy(dependency => dependency.PredecessorStage!.SequenceNo)
            .Select(dependency => new RoutingDependencyResponse
            {
                PredecessorSequenceNo = dependency.PredecessorStage!.SequenceNo,
                SuccessorSequenceNo = dependency.SuccessorStage!.SequenceNo,
                MinLagMinutes = dependency.MinLagMinutes,
            })
            .ToListAsync(cancellationToken);
        return new RoutingResponse { Stages = stages, Dependencies = dependencies };
    }

    // Holds the product row until the transaction ends, so two saves of the same routing run
    // one after the other and the product cannot be deleted in between.
    private async Task LockProductAsync(int productId, CancellationToken cancellationToken)
    {
        if (!await context.LockRowAsync<Product>(productId, cancellationToken))
        {
            throw ProductNotFound();
        }
    }

    private async Task RejectInvalidStagesAsync(IReadOnlyList<RoutingStageRequest> stages, CancellationToken cancellationToken)
    {
        Dictionary<string, IReadOnlyList<string>> fieldErrors = [];
        if (!RoutingRules.AreNumberedFromOneWithoutGaps(stages.Select(stage => stage.SequenceNo.Validated())))
        {
            fieldErrors["stages"] = ["The stage sequence numbers must be 1, 2, 3 and so on, without gaps or repeats."];
        }

        int[] requestedTypeIds = stages.Select(stage => stage.ResourceTypeId.Validated()).Distinct().ToArray();
        List<int> existingTypeIds = await context.ResourceTypes
            .Where(type => requestedTypeIds.Contains(type.Id))
            .Select(type => type.Id)
            .ToListAsync(cancellationToken);
        for (int position = 0; position < stages.Count; position++)
        {
            if (!existingTypeIds.Contains(stages[position].ResourceTypeId.Validated()))
            {
                fieldErrors[$"stages[{position}].resourceTypeId"] = ["This machine type does not exist."];
            }
        }

        if (fieldErrors.Count > 0)
        {
            throw ApiProblemException.ValidationFailed(fieldErrors);
        }
    }

    private static ApiProblemException ProductNotFound() => ApiProblemException.NotFound("This product");
}
