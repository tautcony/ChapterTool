using ChapterTool.Core.Models;

namespace ChapterTool.Core.Transform;

/// <summary>
/// Defines frame rate lookup, detection, and frame metadata operations.
/// </summary>
public interface IFrameRateService
{
    /// <summary>
    /// Gets the known frame rate options.
    /// </summary>
    IReadOnlyList<FrameRateOption> Options { get; }

    /// <summary>
    /// Finds a frame rate option by numeric value.
    /// </summary>
    /// <param name="framesPerSecond">The frame rate in frames per second.</param>
    /// <returns>The matching frame rate option, or an invalid option when no exact match exists.</returns>
    FrameRateOption FindByValue(decimal framesPerSecond);

    /// <summary>
    /// Detects the most likely frame rate for chapter data.
    /// </summary>
    /// <param name="info">The chapter data to inspect.</param>
    /// <param name="tolerance">The acceptable frame deviation tolerance.</param>
    /// <returns>The detected frame rate option.</returns>
    FrameRateOption Detect(ChapterSet info, decimal tolerance);

    /// <summary>
    /// Detects the most likely frame rate and returns confidence details.
    /// </summary>
    /// <param name="info">The chapter data to inspect.</param>
    /// <param name="tolerance">The acceptable frame deviation tolerance.</param>
    /// <returns>The detailed frame rate detection result.</returns>
    FrameRateDetectionResult DetectDetailed(ChapterSet info, decimal tolerance);

    /// <summary>
    /// Calculates frame numbers and accuracy for chapter data.
    /// </summary>
    /// <param name="info">The chapter data to inspect.</param>
    /// <param name="option">The selected frame rate option.</param>
    /// <param name="round">Whether to round frame numbers.</param>
    /// <param name="tolerance">The acceptable frame deviation tolerance.</param>
    /// <returns>The calculated frame information.</returns>
    FrameInfoResult UpdateFrames(
        ChapterSet info,
        FrameRateOption option,
        bool round,
        decimal tolerance);
}
