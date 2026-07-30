namespace ChapterTool.Core.Importing.Disc;

internal sealed record MplsPipMetadata(
    ushort ClipReference,
    byte SecondaryVideoReference,
    byte TimelineType,
    bool LumaKeyFlag,
    byte UpperLimitLumaKey,
    bool TrickPlayFlag,
    IReadOnlyList<MplsPipData> Data);

internal sealed record MplsPipData(
    uint Time,
    ushort XPosition,
    ushort YPosition,
    byte ScaleFactor);

internal sealed record MplsStaticMetadata(
    byte DynamicRangeType,
    ushort[] DisplayPrimariesX,
    ushort[] DisplayPrimariesY,
    ushort WhitePointX,
    ushort WhitePointY,
    ushort MaxDisplayMasteringLuminance,
    ushort MinDisplayMasteringLuminance,
    ushort MaxContentLightLevel,
    ushort MaxFrameAverageLightLevel);
