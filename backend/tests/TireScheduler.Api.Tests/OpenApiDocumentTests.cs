using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace TireScheduler.Api.Tests;

// The frontend client is generated from backend/openapi/tire-scheduler-v1.json
// (ARCHITECTURE.md section 4). This test keeps that file equal to what the code describes.
// After an intended API change, write the file again with: UPDATE_OPENAPI=1 dotnet test ...
public sealed class OpenApiDocumentTests(SchedulerApiFixture api) : IClassFixture<SchedulerApiFixture>
{
    private const string UpdateVariable = "UPDATE_OPENAPI";

    [Fact]
    public async Task Committed_openapi_document_matches_the_api()
    {
        IOpenApiDocumentProvider documentProvider = api.Factory.Services.GetRequiredKeyedService<IOpenApiDocumentProvider>("v1");
        OpenApiDocument document = await documentProvider.GetOpenApiDocumentAsync(TestContext.Current.CancellationToken);
        string currentDocument = await document.SerializeAsJsonAsync(OpenApiSpecVersion.OpenApi3_1, TestContext.Current.CancellationToken) + "\n";

        string committedPath = CommittedDocumentPath();
        if (Environment.GetEnvironmentVariable(UpdateVariable) == "1")
        {
            await File.WriteAllTextAsync(committedPath, currentDocument, TestContext.Current.CancellationToken);
            return;
        }

        string committedDocument = await File.ReadAllTextAsync(committedPath, TestContext.Current.CancellationToken);
        Assert.True(
            committedDocument == currentDocument,
            $"{committedPath} is out of date. Run the API tests once with {UpdateVariable}=1, then `npm run generate:api` in frontend/.");
    }

    private static string CommittedDocumentPath([CallerFilePath] string thisFilePath = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFilePath)!, "..", "..", "openapi", "tire-scheduler-v1.json"));
}
