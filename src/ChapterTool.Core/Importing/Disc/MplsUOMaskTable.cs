namespace ChapterTool.Core.Importing.Disc;

internal sealed record MplsUOMaskTable(byte[] FlagField)
{
    /// <summary>
    /// Gets the MenuCall value.
    /// </summary>
    public bool MenuCall => Bit(0);
    /// <summary>
    /// Gets the TitleSearch value.
    /// </summary>
    public bool TitleSearch => Bit(1);
    /// <summary>
    /// Gets the ChapterSearch value.
    /// </summary>
    public bool ChapterSearch => Bit(2);
    /// <summary>
    /// Gets the TimeSearch value.
    /// </summary>
    public bool TimeSearch => Bit(3);
    /// <summary>
    /// Gets the SkipToNextPoint value.
    /// </summary>
    public bool SkipToNextPoint => Bit(4);
    /// <summary>
    /// Gets the SkipToPrevPoint value.
    /// </summary>
    public bool SkipToPrevPoint => Bit(5);
    /// <summary>
    /// Gets the Stop value.
    /// </summary>
    public bool Stop => Bit(7);
    /// <summary>
    /// Gets the PauseOn value.
    /// </summary>
    public bool PauseOn => Bit(8);
    /// <summary>
    /// Gets the StillOff value.
    /// </summary>
    public bool StillOff => Bit(10);
    /// <summary>
    /// Gets the ForwardPlay value.
    /// </summary>
    public bool ForwardPlay => Bit(11);
    /// <summary>
    /// Gets the BackwardPlay value.
    /// </summary>
    public bool BackwardPlay => Bit(12);
    /// <summary>
    /// Gets the Resume value.
    /// </summary>
    public bool Resume => Bit(13);
    /// <summary>
    /// Gets the MoveUpSelectedButton value.
    /// </summary>
    public bool MoveUpSelectedButton => Bit(14);
    /// <summary>
    /// Gets the MoveDownSelectedButton value.
    /// </summary>
    public bool MoveDownSelectedButton => Bit(15);
    /// <summary>
    /// Gets the MoveLeftSelectedButton value.
    /// </summary>
    public bool MoveLeftSelectedButton => Bit(16);
    /// <summary>
    /// Gets the MoveRightSelectedButton value.
    /// </summary>
    public bool MoveRightSelectedButton => Bit(17);
    /// <summary>
    /// Gets the SelectButton value.
    /// </summary>
    public bool SelectButton => Bit(18);
    /// <summary>
    /// Gets the ActivateButton value.
    /// </summary>
    public bool ActivateButton => Bit(19);
    /// <summary>
    /// Gets the SelectAndActivateButton value.
    /// </summary>
    public bool SelectAndActivateButton => Bit(20);
    /// <summary>
    /// Gets the PrimaryAudioStreamNumberChange value.
    /// </summary>
    public bool PrimaryAudioStreamNumberChange => Bit(21);
    /// <summary>
    /// Gets the AngleNumberChange value.
    /// </summary>
    public bool AngleNumberChange => Bit(23);
    /// <summary>
    /// Gets the PopupOn value.
    /// </summary>
    public bool PopupOn => Bit(24);
    /// <summary>
    /// Gets the PopupOff value.
    /// </summary>
    public bool PopupOff => Bit(25);
    /// <summary>
    /// Gets the PrimaryPGEnableDisable value.
    /// </summary>
    public bool PrimaryPGEnableDisable => Bit(26);
    /// <summary>
    /// Gets the PrimaryPGStreamNumberChange value.
    /// </summary>
    public bool PrimaryPGStreamNumberChange => Bit(27);
    /// <summary>
    /// Gets the SecondaryVideoEnableDisable value.
    /// </summary>
    public bool SecondaryVideoEnableDisable => Bit(28);
    /// <summary>
    /// Gets the SecondaryVideoStreamNumberChange value.
    /// </summary>
    public bool SecondaryVideoStreamNumberChange => Bit(29);
    /// <summary>
    /// Gets the SecondaryAudioEnableDisable value.
    /// </summary>
    public bool SecondaryAudioEnableDisable => Bit(30);
    /// <summary>
    /// Gets the SecondaryAudioStreamNumberChange value.
    /// </summary>
    public bool SecondaryAudioStreamNumberChange => Bit(31);
    /// <summary>
    /// Gets the SecondaryPGStreamNumberChange value.
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
