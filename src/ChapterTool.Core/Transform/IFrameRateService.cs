using ChapterTool.Core.Models;

namespace ChapterTool.Core.Transform;

public interface IFrameRateService
{
    IReadOnlyList<FrameRateOption> Options { get; }

    FrameRateOption FindByValue(decimal framesPerSecond);

    FrameRateOption Detect(ChapterInfo info, decimal tolerance);

    FrameRateDetectionResult DetectDetailed(ChapterInfo info, decimal tolerance);

    FrameInfoResult UpdateFrames(
        ChapterInfo info,
        FrameRateOption option,
        bool round,
        decimal tolerance);
}
