using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using ChapterTool.Avalonia.Localization;
using ChapterTool.Avalonia.Views.Controls.Expression;
using ChapterTool.Core.Transform;

namespace ChapterTool.Avalonia.Views.Controls;

public sealed class ExpressionEditorExpansionChangedEventArgs(double heightDelta) : EventArgs
{
    public double HeightDelta { get; } = heightDelta;
}


// Compatibility name used by ExpressionEditor.axaml bindings.
public sealed class ExpressionCompletionKindBrushConverter : Expression.ExpressionCompletionPresentation
{
}

public sealed partial class ExpressionEditor : UserControl
{
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<ExpressionEditor, string>(nameof(Text), "t", defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<IAppLocalizer?> LocalizerProperty =
        AvaloniaProperty.Register<ExpressionEditor, IAppLocalizer?>(nameof(Localizer));

    public static readonly StyledProperty<double> EditorHeightProperty =
        AvaloniaProperty.Register<ExpressionEditor, double>(nameof(EditorHeight), 25.6);

    public static readonly StyledProperty<bool> IsMultilineExpandableProperty =
        AvaloniaProperty.Register<ExpressionEditor, bool>(nameof(IsMultilineExpandable));

    private readonly IExpressionAuthoringService authoringService = new ExpressionAuthoringService();
    private readonly TextEditor editor;
    private readonly Expression.ExpressionColorizer colorizer;
    private readonly Expression.ExpressionDiagnosticPresentation diagnosticRenderer;
    private readonly DispatcherTimer diagnosticHideTimer;
    private readonly DispatcherTimer diagnosticRenderTimer;
    private bool updatingText;
    private bool settingEditorTextFromProperty;
    private bool shouldShowCompletion;
    private bool isPointerOverDiagnosticPopup;
    private bool isMultilineExpanded;
    private string diagnosticTooltipText = string.Empty;
    private ExpressionAuthoringDiagnostic? primaryDiagnostic;

    public ExpressionEditor()
    {
        InitializeComponent();

        colorizer = new Expression.ExpressionColorizer([]);
        diagnosticRenderer = new Expression.ExpressionDiagnosticPresentation();
        diagnosticHideTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(150)
        };
        diagnosticHideTimer.Tick += OnDiagnosticHideTimerTick;
        diagnosticRenderTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        diagnosticRenderTimer.Tick += OnDiagnosticRenderTimerTick;

        editor = new TextEditor
        {
            ShowLineNumbers = false,
            WordWrap = false,
            FontSize = 13,
            Background = Brushes.Transparent,
            Foreground = Expression.ExpressionThemeBrushes.EditorForeground,
            Padding = new Thickness(6, 3),
            MinHeight = EditorHeight,
            Height = EditorHeight,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Document = new TextDocument(Text)
        };
        editor.TextArea.TextView.LineTransformers.Add(colorizer);
        editor.TextArea.TextView.BackgroundRenderers.Add(diagnosticRenderer);
        editor.TextChanged += OnEditorTextChanged;
        editor.TextArea.Caret.PositionChanged += OnCaretPositionChanged;
        editor.TextArea.AddHandler(KeyDownEvent, OnEditorPreviewKeyDown, RoutingStrategies.Tunnel);
        editor.TextArea.TextInput += OnEditorTextInput;
        editor.TextArea.TextView.PointerMoved += OnTextViewPointerMoved;
        editor.TextArea.TextView.PointerExited += OnTextViewPointerExited;
        editor.GotFocus += OnEditorFocusChanged;
        editor.LostFocus += OnEditorFocusChanged;
        editor.TextArea.GotFocus += OnEditorFocusChanged;
        editor.TextArea.LostFocus += OnEditorFocusChanged;
        editor.TextArea.Focusable = true;
        EditorHost.Content = editor;
        UpdateMultilineState();
        AnalyzeAndRender(renderDiagnosticsImmediately: true);
    }

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public IAppLocalizer? Localizer
    {
        get => GetValue(LocalizerProperty);
        set => SetValue(LocalizerProperty, value);
    }

    public double EditorHeight
    {
        get => GetValue(EditorHeightProperty);
        set => SetValue(EditorHeightProperty, value);
    }

    public bool IsMultilineExpandable
    {
        get => GetValue(IsMultilineExpandableProperty);
        set => SetValue(IsMultilineExpandableProperty, value);
    }

    public event EventHandler<ExpressionEditorExpansionChangedEventArgs>? MultilineExpansionChanged;

    public IReadOnlyList<ExpressionCompletion> CurrentCompletions { get; private set; } = [];

    public string EditorText => editor.Text;

    public string DiagnosticTooltipText => diagnosticTooltipText;

    public bool HasDiagnostic => !string.IsNullOrWhiteSpace(diagnosticTooltipText);

    public bool IsCompletionOpen => CompletionPopup.IsOpen;

    public bool IsDiagnosticOpen => DiagnosticPopup.IsOpen;

    public double ActualEditorHeightForTesting => editor.Height;

    public int CaretOffsetForTesting => editor.CaretOffset;

    public bool IsMultilineExpanded
    {
        get => isMultilineExpanded;
        set
        {
            if (isMultilineExpanded == value)
            {
                return;
            }

            SetMultilineExpanded(value);
        }
    }

    public void MoveCaretToEnd()
    {
        editor.CaretOffset = editor.Document.TextLength;
        FocusInnerEditor();
        AnalyzeAndRender(renderDiagnosticsImmediately: true);
    }

    public void SetCaretOffsetForTesting(int offset)
    {
        FocusInnerEditor();
        editor.SelectionLength = 0;
        editor.CaretOffset = Math.Clamp(offset, 0, editor.Document.TextLength);
        AnalyzeAndRender();
    }

    public void InsertTextForTesting(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        FocusInnerEditor();
        shouldShowCompletion = true;
        var offset = editor.CaretOffset;
        editor.Document.Insert(offset, text);
        editor.CaretOffset = Math.Clamp(offset + text.Length, 0, editor.Document.TextLength);
        AnalyzeAndRender(renderDiagnosticsImmediately: false);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty && !updatingText)
        {
            var value = change.NewValue as string ?? string.Empty;
            if (editor.Text != value)
            {
                settingEditorTextFromProperty = true;
                try
                {
                    editor.Text = value;
                }
                finally
                {
                    settingEditorTextFromProperty = false;
                }
            }

            UpdateMultilineState();
            AnalyzeAndRender(renderDiagnosticsImmediately: true);
        }
        else if (change.Property == LocalizerProperty)
        {
            AnalyzeAndRender();
        }
        else if (change.Property == EditorHeightProperty && editor is not null)
        {
            var height = Math.Max(25.6, change.GetNewValue<double>());
            editor.MinHeight = height;
            editor.Height = height;
            UpdateMultilineState();
        }
        else if (change.Property == IsMultilineExpandableProperty && editor is not null)
        {
            UpdateMultilineState();
        }
    }

    public void AcceptFirstCompletion()
    {
        if (CurrentCompletions.Count == 0)
        {
            return;
        }

        InsertCompletion(CurrentCompletions[0]);
    }

    private void OnEditorTextChanged(object? sender, EventArgs args)
    {
        updatingText = true;
        SetCurrentValue(TextProperty, editor.Text);
        updatingText = false;
        shouldShowCompletion = true;
        if (IsMultilineExpandable
            && !settingEditorTextFromProperty
            && !isMultilineExpanded
            && HasMultipleLines(editor.Text))
        {
            SetMultilineExpanded(true);
        }
        else
        {
            UpdateMultilineState();
        }

        AnalyzeAndRender(renderDiagnosticsImmediately: false);
    }

    private void OnMultilineToggleClick(object? sender, RoutedEventArgs args)
    {
        IsMultilineExpanded = !IsMultilineExpanded;
        args.Handled = true;
        Dispatcher.UIThread.Post(FocusInnerEditor);
    }

    private void UpdateMultilineState()
    {
        if (editor is null)
        {
            return;
        }

        var hasMultipleLines = HasMultipleLines(editor.Text);
        SetMultilineExpanded(isMultilineExpanded && IsMultilineExpandable && hasMultipleLines);
    }

    private void SetMultilineExpanded(bool value)
    {
        var canExpand = IsMultilineExpandable && HasMultipleLines(editor.Text);
        var nextValue = value && canExpand;
        var changed = isMultilineExpanded != nextValue;
        var previousHeight = editor.Height;
        isMultilineExpanded = nextValue;

        MultilineToggleButton.IsVisible = canExpand;
        MultilineExpandIcon.IsVisible = !isMultilineExpanded;
        MultilineCollapseIcon.IsVisible = isMultilineExpanded;

        var requestedHeight = Math.Max(25.6, EditorHeight);
        var effectiveHeight = canExpand
            ? isMultilineExpanded ? 132 : 25.6
            : requestedHeight;

        editor.MinHeight = effectiveHeight;
        editor.Height = effectiveHeight;
        editor.VerticalScrollBarVisibility = isMultilineExpanded || requestedHeight > 25.6
            ? ScrollBarVisibility.Auto
            : ScrollBarVisibility.Disabled;

        if (changed)
        {
            MultilineExpansionChanged?.Invoke(
                this,
                new ExpressionEditorExpansionChangedEventArgs(effectiveHeight - previousHeight));
        }
    }

    private static bool HasMultipleLines(string text) => text.Contains('\n') || text.Contains('\r');

    private void OnCaretPositionChanged(object? sender, EventArgs args)
    {
        AnalyzeAndRender(renderDiagnosticsImmediately: !diagnosticRenderTimer.IsEnabled);
    }

    private void OnEditorPreviewKeyDown(object? sender, KeyEventArgs args)
    {
        HandleCompletionKeys(args);
    }

    private void HandleCompletionKeys(KeyEventArgs args)
    {
        if (args.Handled)
        {
            return;
        }

        var hasCommandModifier = args.KeyModifiers.HasFlag(KeyModifiers.Control)
            || args.KeyModifiers.HasFlag(KeyModifiers.Alt)
            || args.KeyModifiers.HasFlag(KeyModifiers.Meta);
        if (!hasCommandModifier
            && (args.Key == Key.Add
                || (args.Key == Key.OemPlus && args.KeyModifiers.HasFlag(KeyModifiers.Shift))))
        {
            ReplaceSelection("+");
            args.Handled = true;
            return;
        }

        if (args.Key == Key.Down && CompletionPopup.IsOpen && CurrentCompletions.Count > 0)
        {
            MoveCompletionSelection(1);
            args.Handled = true;
            return;
        }

        if (args.Key == Key.Up && CompletionPopup.IsOpen && CurrentCompletions.Count > 0)
        {
            MoveCompletionSelection(-1);
            args.Handled = true;
            return;
        }

        if (args.Key == Key.Tab && CompletionPopup.IsOpen && CurrentCompletions.Count > 0)
        {
            AcceptSelectedCompletion();
            args.Handled = true;
            return;
        }

        if (args.Key is Key.Escape)
        {
            var wasOpen = CompletionPopup.IsOpen;
            CloseCompletionPopup();
            args.Handled = wasOpen;
        }
    }

    private void ReplaceSelection(string text)
    {
        var start = editor.SelectionStart;
        editor.Document.Replace(start, editor.SelectionLength, text);
        editor.SelectionLength = 0;
        editor.CaretOffset = start + text.Length;
    }

    private void OnEditorTextInput(object? sender, TextInputEventArgs args)
    {
        shouldShowCompletion = true;
    }

    private void OnEditorFocusChanged(object? sender, RoutedEventArgs args)
    {
        if (!editor.IsKeyboardFocusWithin)
        {
            CloseDiagnosticPopup();
        }

        RenderCompletionPopup();
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs args)
    {
        if ((args.Source as Visual)?.FindAncestorOfType<TextEditor>() is not null)
        {
            return;
        }

        Dispatcher.UIThread.Post(FocusInnerEditor);
    }

    private void OnWrapperGotFocus(object? sender, RoutedEventArgs args)
    {
        if (!editor.IsKeyboardFocusWithin)
        {
            FocusInnerEditor();
        }
    }

    private void FocusInnerEditor()
    {
        editor.Focus();
        editor.TextArea.Focus();
        var caretOffset = Math.Clamp(editor.CaretOffset, 0, editor.Document.TextLength);
        if (editor.CaretOffset != caretOffset)
        {
            editor.CaretOffset = caretOffset;
        }
    }

    private void AnalyzeAndRender(bool renderDiagnosticsImmediately = true)
    {
        var result = authoringService.Analyze(editor.Text, editor.CaretOffset);
        CurrentCompletions = result.Completions;
        colorizer.Update(result.Spans);
        editor.TextArea.TextView.Redraw();
        if (renderDiagnosticsImmediately || result.Diagnostics.Count == 0)
        {
            diagnosticRenderTimer.Stop();
            RenderDiagnostics(result);
        }
        else
        {
            diagnosticRenderTimer.Stop();
            diagnosticRenderTimer.Start();
        }

        RenderCompletionPopup();
    }

    private void OnDiagnosticRenderTimerTick(object? sender, EventArgs args)
    {
        diagnosticRenderTimer.Stop();
        var result = authoringService.Analyze(editor.Text, editor.CaretOffset);
        RenderDiagnostics(result);
    }

    private void RenderDiagnostics(ExpressionAnalysisResult result)
    {
        diagnosticRenderer.Update(result.Diagnostics);
        editor.TextArea.TextView.Redraw();
        primaryDiagnostic = result.Diagnostics.FirstOrDefault(static diagnostic => diagnostic.Length > 0) ?? result.Diagnostics.FirstOrDefault();
        var hasDiagnostic = primaryDiagnostic is not null;
        diagnosticTooltipText = hasDiagnostic
            ? LocalizeDiagnostic(primaryDiagnostic!.Diagnostic.DisplayCode, primaryDiagnostic.Diagnostic.Message, primaryDiagnostic.Diagnostic.Arguments) + Environment.NewLine + LocalizeSuggestion(primaryDiagnostic.Suggestion)
            : string.Empty;
        DiagnosticTextBlock.Text = diagnosticTooltipText;
        if (!hasDiagnostic)
        {
            CloseDiagnosticPopup();
        }
    }

    private void RenderCompletionPopup()
    {
        if (!shouldShowCompletion
            || (!editor.IsKeyboardFocusWithin && !IsCompletionPopupInteractionActive())
            || CurrentCompletions.Count == 0)
        {
            CloseCompletionPopup();
            return;
        }

        CompletionList.ItemsSource = CurrentCompletions;
        if (CompletionList.SelectedIndex < 0)
        {
            CompletionList.SelectedIndex = 0;
        }

        var completionAnchor = GetCompletionAnchorOffset();
        CompletionPopup.PlacementTarget = RootGrid;
        CompletionPopup.HorizontalOffset = completionAnchor.X + 12;
        CompletionPopup.VerticalOffset = RootGrid.Bounds.Height + 4;
        CompletionPopup.IsOpen = true;
        CloseDiagnosticPopup();
    }

    private bool IsCompletionPopupInteractionActive()
    {
        return CompletionPopup.IsOpen
            && CompletionPanel.IsPointerOver;
    }

    private void InsertCompletion(ExpressionCompletion completion)
    {
        editor.Document.Replace(completion.ReplacementStart, completion.ReplacementLength, completion.InsertText);
        var caret = completion.ReplacementStart + completion.InsertText.Length;
        if (completion.InsertText.EndsWith("()", StringComparison.Ordinal))
        {
            caret--;
        }
        else if (completion.InsertText.Contains("(, )", StringComparison.Ordinal))
        {
            caret = completion.ReplacementStart + completion.InsertText.IndexOf("(, )", StringComparison.Ordinal) + 1;
        }

        editor.CaretOffset = Math.Clamp(caret, 0, editor.Document.TextLength);
        FocusInnerEditor();
        CloseCompletionPopup();
        shouldShowCompletion = false;
        AnalyzeAndRender();
    }

    private void CloseCompletionPopup()
    {
        CompletionPopup.IsOpen = false;
        CompletionList.ItemsSource = null;
        CompletionList.SelectedIndex = -1;
        shouldShowCompletion = false;
    }

    private void AcceptSelectedCompletion()
    {
        if (CurrentCompletions.Count == 0)
        {
            return;
        }
        var selected = CompletionList.SelectedItem as ExpressionCompletion ?? CurrentCompletions[0];

        InsertCompletion(selected);
    }

    private void MoveCompletionSelection(int delta)
    {
        if (CurrentCompletions.Count == 0)
        {
            return;
        }

        var next = CompletionList.SelectedIndex < 0
            ? 0
            : Math.Clamp(CompletionList.SelectedIndex + delta, 0, CurrentCompletions.Count - 1);
        CompletionList.SelectedIndex = next;
        if (CompletionList.SelectedItem is not null)
        {
            CompletionList.ScrollIntoView(CompletionList.SelectedItem);
        }
    }

    private Point GetCaretAnchorOffset()
    {
        var textView = editor.TextArea.TextView;
        textView.EnsureVisualLines();

        var rectangle = editor.TextArea.Caret.CalculateCaretRectangle();
        var origin = editor.TranslatePoint(default, RootGrid) ?? default;
        return new Point(
            origin.X + rectangle.X,
            origin.Y + rectangle.Bottom);
    }

    private Point GetCompletionAnchorOffset()
    {
        if (CurrentCompletions.Count == 0)
        {
            return new Point(GetCaretAnchorOffset().X, RootGrid.Bounds.Height);
        }

        var completion = CompletionList.SelectedItem as ExpressionCompletion ?? CurrentCompletions[0];
        var anchor = GetDocumentOffsetAnchorOffset(completion.ReplacementStart + completion.ReplacementLength);
        return new Point(anchor.X, RootGrid.Bounds.Height);
    }

    private Point GetDiagnosticAnchorOffset(int offset)
    {
        return GetDocumentOffsetAnchorOffset(offset);
    }

    private Point GetDocumentOffsetAnchorOffset(int offset)
    {
        var textView = editor.TextArea.TextView;
        textView.EnsureVisualLines();
        var location = editor.Document.GetLocation(Math.Clamp(offset, 0, editor.Document.TextLength));
        var visualPoint = textView.GetVisualPosition(new TextViewPosition(location), VisualYPosition.LineBottom);
        var origin = textView.TranslatePoint(default, RootGrid) ?? default;
        return new Point(origin.X + visualPoint.X, origin.Y + visualPoint.Y);
    }

    private void OnTextViewPointerMoved(object? sender, PointerEventArgs args)
    {
        if (primaryDiagnostic is null || primaryDiagnostic.Length <= 0 || CompletionPopup.IsOpen)
        {
            CloseDiagnosticPopup();
            return;
        }

        var textView = editor.TextArea.TextView;
        textView.EnsureVisualLines();
        var position = textView.GetPosition(args.GetPosition(textView));
        if (position is null)
        {
            ScheduleDiagnosticPopupHide();
            return;
        }

        var offset = editor.Document.GetOffset(position.Value.Line, position.Value.Column);
        var endOffset = primaryDiagnostic.Start + primaryDiagnostic.Length;
        if (offset < primaryDiagnostic.Start || offset > endOffset)
        {
            ScheduleDiagnosticPopupHide();
            return;
        }

        diagnosticHideTimer.Stop();
        DiagnosticPopup.PlacementTarget = EditorBorder;
        DiagnosticPopup.HorizontalOffset = 8;
        DiagnosticPopup.VerticalOffset = 4;
        DiagnosticPopup.IsOpen = true;
    }

    private void OnTextViewPointerExited(object? sender, PointerEventArgs args)
    {
        ScheduleDiagnosticPopupHide();
    }

    private void OnDiagnosticPanelPointerEntered(object? sender, PointerEventArgs args)
    {
        isPointerOverDiagnosticPopup = true;
        diagnosticHideTimer.Stop();
    }

    private void OnDiagnosticPanelPointerExited(object? sender, PointerEventArgs args)
    {
        isPointerOverDiagnosticPopup = false;
        ScheduleDiagnosticPopupHide();
    }

    private void OnDiagnosticHideTimerTick(object? sender, EventArgs args)
    {
        diagnosticHideTimer.Stop();
        if (!isPointerOverDiagnosticPopup)
        {
            DiagnosticPopup.IsOpen = false;
        }
    }

    private void ScheduleDiagnosticPopupHide()
    {
        if (isPointerOverDiagnosticPopup)
        {
            return;
        }

        diagnosticHideTimer.Stop();
        diagnosticHideTimer.Start();
    }

    private void CloseDiagnosticPopup()
    {
        diagnosticHideTimer.Stop();
        isPointerOverDiagnosticPopup = false;
        DiagnosticPopup.IsOpen = false;
    }

    private void OnCompletionSelectionChanged(object? sender, SelectionChangedEventArgs args)
    {
        shouldShowCompletion = CurrentCompletions.Count > 0;
    }

    private void OnCompletionListPointerPressed(object? sender, PointerPressedEventArgs args)
    {
        if (!CompletionPopup.IsOpen || !args.GetCurrentPoint(CompletionList).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var item = (args.Source as Visual)?.FindAncestorOfType<ListBoxItem>();
        if (item?.DataContext is not ExpressionCompletion completion)
        {
            return;
        }

        args.Handled = true;
        CompletionList.SelectedItem = completion;
        InsertCompletion(completion);
    }

    private void OnCompletionListDoubleTapped(object? sender, RoutedEventArgs args)
    {
        if (!CompletionPopup.IsOpen)
        {
            return;
        }

        args.Handled = true;
        AcceptSelectedCompletion();
    }

    private string LocalizeDiagnostic(string code, string fallback, IReadOnlyDictionary<string, object?>? arguments)
    {
        var localizer = Localizer;
        if (localizer is null)
        {
            return fallback;
        }

        var message = localizer.Format($"Diagnostic.{code}", arguments ?? new Dictionary<string, object?>());
        return string.Equals(message, $"Diagnostic.{code}", StringComparison.Ordinal) ? fallback : message;
    }

    private string LocalizeSuggestion(ExpressionDiagnosticSuggestion suggestion)
    {
        var localizer = Localizer;
        if (localizer is null)
        {
            return suggestion.Message;
        }

        var message = localizer.GetString(suggestion.Code);
        return string.Equals(message, suggestion.Code, StringComparison.Ordinal) ? suggestion.Message : message;
    }

}
