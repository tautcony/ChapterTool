using System.Runtime.Versioning;
using ChapterTool.Core.Editing;
using ChapterTool.Core.Session;
using ChapterTool.Wasm.Services;

namespace ChapterTool.Wasm.Tests;

[SupportedOSPlatform("browser")]
public sealed class WasmHistoryParityTests
{
    [Fact]
    public async Task Browser_cell_commit_records_localized_operation_and_actual_field_value()
    {
        using var workspace = CreateWorkspace();
        await workspace.LoadSampleAsync();
        var draft = workspace.BeginCellEdit(0, ChapterCellField.Name)!;
        Assert.Null(await workspace.CommitCellEditAsync(draft, "Browser edit"));
        var details = Assert.IsType<HistoryNodeDetails>(
            (await workspace.InspectHistoryAsync(workspace.CurrentHistoryId!.Value)).Details);
        var projection = WasmHistoryProjection.Project(new WasmLocalizer(), details);
        Assert.Equal("Edit chapter · Name", projection.OperationTitle);
        var change = Assert.Single(projection.Changes, row => row.Field == "Name");
        Assert.Equal("Opening", change.BeforeText);
        Assert.Equal("Browser edit", change.AfterText);
    }

    [Fact]
    public async Task History_projection_localizes_root_and_complete_change_details_without_moving_cursor()
    {
        using var workspace = CreateWorkspace();
        await workspace.LoadSampleAsync();
        var rootId = workspace.HistoryRootId;
        var rootResult = await workspace.InspectHistoryAsync(rootId!.Value);
        var rootDetails = Assert.IsType<HistoryNodeDetails>(rootResult.Details);
        var rootProjection = WasmHistoryProjection.Project(new WasmLocalizer(), rootDetails);

        Assert.Equal("Initial document", rootProjection.OperationTitle);
        Assert.NotEmpty(rootProjection.RootChapterRows);
        Assert.Equal(rootProjection.RootChapterRows.Count, rootProjection.RootChapterCount);
        Assert.Equal(rootId, workspace.CurrentHistoryId);

        workspace.UpdateRow(0, null, "Changed title");
        var editedId = workspace.CurrentHistoryId;
        var editedResult = await workspace.InspectHistoryAsync(editedId!.Value);
        var editedDetails = Assert.IsType<HistoryNodeDetails>(editedResult.Details);
        var projection = WasmHistoryProjection.Project(new WasmLocalizer(), editedDetails);
        var nameChange = Assert.Single(projection.Changes, change => change.Field == "Name");

        Assert.Equal("Edit chapter · Name", projection.OperationTitle);
        Assert.NotEqual(nameChange.BeforeText, nameChange.AfterText);
        Assert.Equal("Opening", nameChange.BeforeText);
        Assert.Equal("Changed title", nameChange.AfterText);
        var modified = Assert.Single(WasmHistoryProjection.FilterChanges(projection.Changes, WasmHistoryChangeFilter.Modified));
        Assert.True(modified.Before.IsPresent);
        Assert.True(modified.After.IsPresent);
        Assert.Equal(editedId, workspace.CurrentHistoryId);
    }

    [Fact]
    public void Change_filters_separate_added_removed_and_modified_fields()
    {
        var rows = new[]
        {
            Change("Added", new HistoryFieldValue(false, null), new HistoryFieldValue(true, "value")),
            Change("Removed", new HistoryFieldValue(true, "value"), new HistoryFieldValue(false, null)),
            Change("Modified", new HistoryFieldValue(true, "before"), new HistoryFieldValue(true, "after"))
        };

        Assert.Equal("Added", Assert.Single(WasmHistoryProjection.FilterChanges(rows, WasmHistoryChangeFilter.Added)).Field);
        Assert.Equal("Removed", Assert.Single(WasmHistoryProjection.FilterChanges(rows, WasmHistoryChangeFilter.Removed)).Field);
        Assert.Equal("Modified", Assert.Single(WasmHistoryProjection.FilterChanges(rows, WasmHistoryChangeFilter.Modified)).Field);
        Assert.Equal(3, WasmHistoryProjection.FilterChanges(rows, WasmHistoryChangeFilter.All).Count);
    }

    [Fact]
    public async Task Operation_parameters_use_localized_names_and_typed_values()
    {
        using var workspace = CreateWorkspace();
        await workspace.LoadSampleAsync();
        var details = Assert.IsType<HistoryNodeDetails>((await workspace.InspectHistoryAsync(workspace.HistoryRootId!.Value)).Details);
        var operation = new HistoryOperationDescriptor("chapter.options-apply",
        [
            new HistoryOperationParameter("orderShift", "2", "integer"),
            new HistoryOperationParameter("autoGenerateNames", "true", "boolean"),
            new HistoryOperationParameter("trackId", "hidden", "text")
        ]);

        var projection = WasmHistoryProjection.Project(new WasmLocalizer(), details with { Operation = operation });

        Assert.Equal("Apply chapter options", projection.OperationTitle);
        Assert.Collection(projection.Parameters,
            row =>
            {
                Assert.Equal("Order Shift", row.Name);
                Assert.Equal("2", row.Value);
            },
            row =>
            {
                Assert.Equal("Generate Names", row.Name);
                Assert.Equal("Yes", row.Value);
            });
    }

    [Fact]
    public async Task Selecting_an_alternate_redo_branch_does_not_restore_until_redo()
    {
        using var workspace = CreateWorkspace();
        await workspace.LoadSampleAsync();
        var originalName = workspace.Rows[0].Name;

        workspace.UpdateRow(0, null, "First branch");
        var firstBranch = Assert.Single(workspace.HistoryEntries, static entry => entry.IsCurrent);
        await workspace.UndoAsync();
        workspace.UpdateRow(0, null, "Alternate branch");
        await workspace.UndoAsync();

        var rootCursor = workspace.CurrentHistoryId;
        Assert.Equal(originalName, workspace.Rows[0].Name);
        Assert.Equal(2, workspace.HistoryRedoChoices.Count);
        Assert.False(workspace.SelectHistoryRedoBranch(Guid.NewGuid()));
        Assert.True(workspace.SelectHistoryRedoBranch(firstBranch.Id));
        Assert.Equal(rootCursor, workspace.CurrentHistoryId);
        Assert.Equal(originalName, workspace.Rows[0].Name);
        Assert.Equal(firstBranch.Id, workspace.SelectedHistoryRedoBranchId);

        await workspace.RedoSelectedHistoryBranchAsync();

        Assert.Equal(firstBranch.Id, workspace.CurrentHistoryId);
        Assert.Equal("First branch", workspace.Rows[0].Name);
    }

    [Fact]
    public async Task Selected_redo_branch_is_cleared_when_the_cursor_changes()
    {
        using var workspace = CreateWorkspace();
        await workspace.LoadSampleAsync();
        workspace.UpdateRow(0, null, "First branch");
        var firstBranch = Assert.Single(workspace.HistoryEntries, static entry => entry.IsCurrent);
        await workspace.UndoAsync();
        workspace.UpdateRow(0, null, "Alternate branch");
        await workspace.UndoAsync();
        Assert.True(workspace.SelectHistoryRedoBranch(firstBranch.Id));

        await workspace.RedoAsync();

        Assert.Null(workspace.SelectedHistoryRedoBranchId);
    }

    [Fact]
    public async Task A_pre_cancelled_history_inspection_does_not_publish_or_move_the_cursor()
    {
        using var workspace = CreateWorkspace();
        await workspace.LoadSampleAsync();
        workspace.UpdateRow(0, null, "Inspection target");
        var cursor = workspace.CurrentHistoryId;
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var result = await workspace.InspectHistoryAsync(workspace.HistoryRootId!.Value, cancellation.Token);

        Assert.Equal(HistoryInspectionOutcomeKind.Cancelled, result.Kind);
        Assert.Equal(cursor, workspace.CurrentHistoryId);
        Assert.Equal("Inspection target", workspace.Rows[0].Name);
    }

    private static WasmWorkspace CreateWorkspace() => new(new WasmChapterService());

    private static WasmHistoryChangeRow Change(string field, HistoryFieldValue before, HistoryFieldValue after) =>
        new("Chapter", field, field, field, before, after, "before", "after", null);
}
