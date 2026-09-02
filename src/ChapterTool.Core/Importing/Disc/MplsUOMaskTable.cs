namespace ChapterTool.Core.Importing.Disc;

internal sealed record MplsUOMaskTable(byte[] FlagField)
{
    /// <summary>
    /// Gets a value indicating whether gets the MenuCall value.
    /// </summary>
    public bool MenuCall => Bit(0);
    /// <summary>
    /// Gets a value indicating whether gets the TitleSearch value.
    /// </summary>
    public bool TitleSearch => Bit(1);
    /// <summary>
    /// Gets a value indicating whether gets the ChapterSearch value.
    /// </summary>
    public bool ChapterSearch => Bit(2);
    /// <summary>
    /// Gets a value indicating whether gets the TimeSearch value.
    /// </summary>
    public bool TimeSearch => Bit(3);
    /// <summary>
    /// Gets a value indicating whether gets the SkipToNextPoint value.
    /// </summary>
    public bool SkipToNextPoint => Bit(4);
    /// <summary>
    /// Gets a value indicating whether gets the SkipToPrevPoint value.
    /// </summary>
    public bool SkipToPrevPoint => Bit(5);
    /// <summary>
    /// Gets a value indicating whether gets the Stop value.
    /// </summary>
    public bool Stop => Bit(7);
    /// <summary>
    /// Gets a value indicating whether gets the PauseOn value.
    /// </summary>
    public bool PauseOn => Bit(8);
    /// <summary>
    /// Gets a value indicating whether gets the StillOff value.
    /// </summary>
    public bool StillOff => Bit(10);
    /// <summary>
    /// Gets a value indicating whether gets the ForwardPlay value.
    /// </summary>
    public bool ForwardPlay => Bit(11);
    /// <summary>
    /// Gets a value indicating whether gets the BackwardPlay value.
    /// </summary>
    public bool BackwardPlay => Bit(12);
    /// <summary>
    /// Gets a value indicating whether gets the Resume value.
    /// </summary>
    public bool Resume => Bit(13);
    /// <summary>
    /// Gets a value indicating whether gets the MoveUpSelectedButton value.
    /// </summary>
    public bool MoveUpSelectedButton => Bit(14);
    /// <summary>
    /// Gets a value indicating whether gets the MoveDownSelectedButton value.
    /// </summary>
    public bool MoveDownSelectedButton => Bit(15);
    /// <summary>
    /// Gets a value indicating whether gets the MoveLeftSelectedButton value.
    /// </summary>
    public bool MoveLeftSelectedButton => Bit(16);
    /// <summary>
    /// Gets a value indicating whether gets the MoveRightSelectedButton value.
    /// </summary>
    public bool MoveRightSelectedButton => Bit(17);
    /// <summary>
    /// Gets a value indicating whether gets the SelectButton value.
    /// </summary>
    public bool SelectButton => Bit(18);
    /// <summary>
    /// Gets a value indicating whether gets the ActivateButton value.
    /// </summary>
    public bool ActivateButton => Bit(19);
    /// <summary>
    /// Gets a value indicating whether gets the SelectAndActivateButton value.
    /// </summary>
    public bool SelectAndActivateButton => Bit(20);
    /// <summary>
    /// Gets a value indicating whether gets the PrimaryAudioStreamNumberChange value.
    /// </summary>
    public bool PrimaryAudioStreamNumberChange => Bit(21);
    /// <summary>
    /// Gets a value indicating whether gets the AngleNumberChange value.
    /// </summary>
    public bool AngleNumberChange => Bit(23);
    /// <summary>
    /// Gets a value indicating whether gets the PopupOn value.
    /// </summary>
    public bool PopupOn => Bit(24);
    /// <summary>
    /// Gets a value indicating whether gets the PopupOff value.
    /// </summary>
    public bool PopupOff => Bit(25);
    /// <summary>
    /// Gets a value indicating whether gets the PrimaryPGEnableDisable value.
    /// </summary>
    public bool PrimaryPGEnableDisable => Bit(26);
    /// <summary>
    /// Gets a value indicating whether gets the PrimaryPGStreamNumberChange value.
    /// </summary>
    public bool PrimaryPGStreamNumberChange => Bit(27);
    /// <summary>
    /// Gets a value indicating whether gets the SecondaryVideoEnableDisable value.
    /// </summary>
    public bool SecondaryVideoEnableDisable => Bit(28);
    /// <summary>
    /// Gets a value indicating whether gets the SecondaryVideoStreamNumberChange value.
    /// </summary>
    public bool SecondaryVideoStreamNumberChange => Bit(29);
    /// <summary>
    /// Gets a value indicating whether gets the SecondaryAudioEnableDisable value.
    /// </summary>
    public bool SecondaryAudioEnableDisable => Bit(30);
    /// <summary>
    /// Gets a value indicating whether gets the SecondaryAudioStreamNumberChange value.
    /// </summary>
    public bool SecondaryAudioStreamNumberChange => Bit(31);
    /// <summary>
    /// Gets a value indicating whether gets the SecondaryPGStreamNumberChange value.
    /// </summary>
    public bool SecondaryPGStreamNumberChange => Bit(33);

    /// <summary>
    /// Executes the Read operation.
    /// </summary>
    /// <param name="stream">The source stream.</param>
    /// <returns>The operation result.</returns>
    public static MplsUOMaskTable Read(Stream stream) =>
        new(stream.ReadExactBytes(8));

    private bool Bit(int index) =>
        (FlagField[index / 8] & (0x80 >> (index % 8))) != 0;
}
