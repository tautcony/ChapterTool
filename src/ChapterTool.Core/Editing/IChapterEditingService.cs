using ChapterTool.Core.Models;

namespace ChapterTool.Core.Editing;

/// <summary>
/// Defines chapter editing operations that return updated chapter data and diagnostics.
/// </summary>
public interface IChapterEditingService
{
    /// <summary>
    /// Edits a chapter time from formatted text.
    /// </summary>
    /// <param name="info">The chapter data to edit.</param>
    /// <param name="index">The zero-based chapter index.</param>
    /// <param name="text">The formatted time text.</param>
    /// <returns>The edit result.</returns>
    ChapterEditResult EditTime(ChapterSet info, int index, string text);

    /// <summary>
    /// Edits a chapter frame number from text.
    /// </summary>
    /// <param name="info">The chapter data to edit.</param>
    /// <param name="index">The zero-based chapter index.</param>
    /// <param name="text">The frame text.</param>
    /// <param name="framesPerSecond">The frame rate in frames per second.</param>
    /// <returns>The edit result.</returns>
    ChapterEditResult EditFrame(ChapterSet info, int index, string text, decimal framesPerSecond);

    /// <summary>
    /// Renames a chapter.
    /// </summary>
    /// <param name="info">The chapter data to edit.</param>
    /// <param name="index">The zero-based chapter index.</param>
    /// <param name="name">The new chapter name.</param>
    /// <returns>The edit result.</returns>
    ChapterEditResult Rename(ChapterSet info, int index, string name);

    /// <summary>
    /// Deletes chapters by index.
    /// </summary>
    /// <param name="info">The chapter data to edit.</param>
    /// <param name="indexes">The zero-based chapter indexes to delete.</param>
    /// <returns>The edit result.</returns>
    ChapterEditResult Delete(ChapterSet info, IReadOnlySet<int> indexes);

    /// <summary>
    /// Inserts a chapter before the specified index.
    /// </summary>
    /// <param name="info">The chapter data to edit.</param>
    /// <param name="index">The zero-based insertion index.</param>
    /// <returns>The edit result.</returns>
    ChapterEditResult InsertBefore(ChapterSet info, int index);

    /// <summary>
    /// Shifts chapter order numbers by the specified amount.
    /// </summary>
    /// <param name="info">The chapter data to edit.</param>
    /// <param name="shift">The order shift.</param>
    /// <returns>The edit result.</returns>
    ChapterEditResult ApplyOrderShift(ChapterSet info, int shift);

    /// <summary>
    /// Applies chapter names from newline-delimited template text.
    /// </summary>
    /// <param name="info">The chapter data to edit.</param>
    /// <param name="templateText">The template text.</param>
    /// <returns>The edit result.</returns>
    ChapterEditResult ApplyTemplate(ChapterSet info, string templateText);

    /// <summary>
    /// Shifts all chapter times forward by a number of frames.
    /// </summary>
    /// <param name="info">The chapter data to edit.</param>
    /// <param name="frames">The frame count.</param>
    /// <param name="framesPerSecond">The frame rate in frames per second.</param>
    /// <returns>The edit result.</returns>
    ChapterEditResult ShiftFramesForward(ChapterSet info, int frames, decimal framesPerSecond);

    /// <summary>
    /// Creates zone text from selected chapters.
    /// </summary>
    /// <param name="info">The chapter data to inspect.</param>
    /// <param name="indexes">The selected zero-based chapter indexes.</param>
    /// <param name="framesPerSecond">The frame rate in frames per second.</param>
    /// <returns>The generated zone text and diagnostics.</returns>
    ChapterZonesResult CreateZones(ChapterSet info, IReadOnlySet<int> indexes, decimal framesPerSecond);
}

/// <summary>
/// Represents zone text generated from selected chapters.
/// </summary>
/// <param name="Zones">The generated zone text for the selected chapters.</param>
/// <param name="Diagnostics">Diagnostics produced while creating zone text.</param>
public sealed record ChapterZonesResult(
    string Zones,
    IReadOnlyList<Diagnostics.ChapterDiagnostic> Diagnostics);
