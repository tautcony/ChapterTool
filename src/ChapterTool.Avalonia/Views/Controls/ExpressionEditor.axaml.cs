using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;
using ChapterTool.Avalonia.Localization;
using ChapterTool.Core.Transform;

namespace ChapterTool.Avalonia.Views.Controls;

public sealed partial class ExpressionEditor : UserControl
{
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<ExpressionEditor, string>(nameof(Text), "t", defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<IAppLocalizer?> LocalizerProperty =
        AvaloniaProperty.Register<ExpressionEditor, IAppLocalizer?>(nameof(Localizer));

    private readonly IExpressionAuthoringService authoringService = new ExpressionAuthoringService();
    private readonly TextEditor editor;
    private readonly ExpressionColorizer colorizer;
    private readonly DiagnosticUnderlineRenderer diagnosticRenderer;
    private readonly DispatcherTimer diagnosticHideTimer;
    private bool updatingText;
    private bool shouldShowCompletion;
    private bool isPointerOverDiagnosticPopup;
    private string diagnosticTooltipText = string.Empty;
    private ExpressionAuthoringDiagnostic? primaryDiagnostic;

    public ExpressionEditor()
    {
        InitializeComponent();

        colorizer = new ExpressionColorizer([]);
        diagnosticRenderer = new DiagnosticUnderlineRenderer();
        diagnosticHideTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(150)
        };
        diagnosticHideTimer.Tick += OnDiagnosticHideTimerTick;
        editor = new TextEditor
        {
            ShowLineNumbers = false,
            WordWrap = false,
            FontFamily = FontFamily.Parse("Menlo, Consolas, monospace"),
            FontSize = 13,
            Background = Brushes.Transparent,
            Foreground = Brush("#24292f"),
            Padding = new Thickness(6, 3),
            MinHeight = 25.6,
            Height = 25.6,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Document = new TextDocument(Text)
        };
        editor.TextArea.TextView.LineTransformers.Add(colorizer);
        editor.TextArea.TextView.BackgroundRenderers.Add(diagnosticRenderer);
        editor.TextChanged += OnEditorTextChanged;
        editor.TextArea.Caret.PositionChanged += OnCaretPositionChanged;
        editor.TextArea.AddHandler(KeyDownEvent, OnEditorPreviewKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        editor.TextArea.KeyDown += OnEditorKeyDown;
        editor.TextArea.TextInput += OnEditorTextInput;
        editor.TextArea.TextView.PointerMoved += OnTextViewPointerMoved;
        editor.TextArea.TextView.PointerExited += OnTextViewPointerExited;
        editor.GotFocus += OnEditorFocusChanged;
        editor.LostFocus += OnEditorFocusChanged;
        editor.TextArea.GotFocus += OnEditorFocusChanged;
        editor.TextArea.LostFocus += OnEditorFocusChanged;
        editor.TextArea.Focusable = true;
        EditorHost.Content = editor;
        AnalyzeAndRender();
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

    public IReadOnlyList<ExpressionCompletion> CurrentCompletions { get; private set; } = [];

    public string EditorText => editor.Text;

    public string DiagnosticTooltipText => diagnosticTooltipText;

    public bool HasDiagnostic => !string.IsNullOrWhiteSpace(diagnosticTooltipText);

    public bool IsCompletionOpen => CompletionPopup.IsOpen;

    public bool IsDiagnosticOpen => DiagnosticPopup.IsOpen;

    public void MoveCaretToEnd()
    {
        editor.CaretOffset = editor.Document.TextLength;
        FocusInnerEditor();
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
        AnalyzeAndRender();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty && !updatingText)
        {
            var value = change.NewValue as string ?? string.Empty;
            if (editor.Text != value)
            {
                editor.Text = value;
            }

            AnalyzeAndRender();
        }
        else if (change.Property == LocalizerProperty)
        {
            AnalyzeAndRender();
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
        AnalyzeAndRender();
    }

    private void OnCaretPositionChanged(object? sender, EventArgs args)
    {
        AnalyzeAndRender();
    }

    private void OnEditorKeyDown(object? sender, KeyEventArgs args)
    {
        HandleCompletionKeys(args);
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

        if ((args.Key == Key.Tab || args.Key == Key.Enter) && CompletionPopup.IsOpen && CurrentCompletions.Count > 0)
        {
            AcceptSelectedCompletion();
            args.Handled = true;
            return;
        }

        if (args.Key is Key.Escape)
        {
            CloseCompletionPopup();
        }
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
        Dispatcher.UIThread.Post(FocusInnerEditor);
    }

    private void OnWrapperGotFocus(object? sender, RoutedEventArgs args)
    {
        FocusInnerEditor();
    }

    private void FocusInnerEditor()
    {
        editor.Focus();
        editor.TextArea.Focus();
        editor.CaretOffset = Math.Clamp(editor.CaretOffset, 0, editor.Document.TextLength);
    }

    private void AnalyzeAndRender()
    {
        var result = authoringService.Analyze(editor.Text, editor.CaretOffset);
        CurrentCompletions = result.Completions;
        colorizer.Update(result.Spans);
        diagnosticRenderer.Update(result.Diagnostics);
        editor.TextArea.TextView.Redraw();
        RenderDiagnostics(result);
        RenderCompletionPopup();
    }

    private void RenderDiagnostics(ExpressionAnalysisResult result)
    {
        primaryDiagnostic = result.Diagnostics.FirstOrDefault(static diagnostic => diagnostic.Length > 0) ?? result.Diagnostics.FirstOrDefault();
        var hasDiagnostic = primaryDiagnostic is not null;
        diagnosticTooltipText = hasDiagnostic
            ? LocalizeDiagnostic(primaryDiagnostic!.Diagnostic.Code, primaryDiagnostic.Diagnostic.Message, primaryDiagnostic.Diagnostic.Arguments) + Environment.NewLine + LocalizeSuggestion(primaryDiagnostic.Suggestion)
            : string.Empty;
        DiagnosticTextBlock.Text = diagnosticTooltipText;
        if (!hasDiagnostic)
        {
            CloseDiagnosticPopup();
        }
    }

    private void RenderCompletionPopup()
    {
        if (!shouldShowCompletion || !editor.IsKeyboardFocusWithin || CurrentCompletions.Count == 0)
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
        var selected = CompletionList.SelectedItem as ExpressionCompletion ?? CurrentCompletions.FirstOrDefault();
        if (selected is null)
        {
            return;
        }

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

    private void OnCompletionListDoubleTapped(object? sender, RoutedEventArgs args)
    {
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

    private static IBrush Brush(string color) => new SolidColorBrush(Color.Parse(color));

    private sealed class ExpressionColorizer(IReadOnlyList<ExpressionTokenSpan> spans) : DocumentColorizingTransformer
    {
        private IReadOnlyList<ExpressionTokenSpan> spans = spans;

        public void Update(IReadOnlyList<ExpressionTokenSpan> value) => spans = value;

        protected override void ColorizeLine(DocumentLine line)
        {
            var lineStart = line.Offset;
            var lineEnd = line.EndOffset;
            foreach (var span in spans)
            {
                var start = span.Start;
                var end = span.Start + span.Length;
                if (end <= lineStart || start >= lineEnd)
                {
                    continue;
                }

                ChangeLinePart(
                    Math.Max(start, lineStart),
                    Math.Min(end, lineEnd),
                    element => element.TextRunProperties.SetForegroundBrush(ForegroundFor(span.Kind)));
            }
        }

        private static IBrush ForegroundFor(ExpressionTokenKind kind) =>
            kind switch
            {
                ExpressionTokenKind.Variable => Brush("#0550ae"),
                ExpressionTokenKind.Constant => Brush("#8250df"),
                ExpressionTokenKind.Function => Brush("#953800"),
                ExpressionTokenKind.Operator => Brush("#cf222e"),
                ExpressionTokenKind.Punctuation => Brush("#57606a"),
                ExpressionTokenKind.Number => Brush("#116329"),
                ExpressionTokenKind.Comment => Brush("#6e7781"),
                ExpressionTokenKind.Unknown => Brush("#b42318"),
                _ => Brush("#24292f")
            };
    }
    
    private sealed class DiagnosticUnderlineRenderer : IBackgroundRenderer
    {
        private IReadOnlyList<ExpressionAuthoringDiagnostic> diagnostics = [];

        public KnownLayer Layer => KnownLayer.Selection;

        public void Update(IReadOnlyList<ExpressionAuthoringDiagnostic> value) => diagnostics = value;

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            if (diagnostics.Count == 0)
            {
                return;
            }

            textView.EnsureVisualLines();
            foreach (var diagnostic in diagnostics.Where(static diagnostic => diagnostic.Length > 0))
            {
                var rects = BackgroundGeometryBuilder.GetRectsForSegment(
                    textView,
                    new Segment(diagnostic.Start, diagnostic.Length),
                    false);

                foreach (var rect in rects)
                {
                    DrawUnderline(drawingContext, rect);
                }
            }
        }

        private static void DrawUnderline(DrawingContext drawingContext, Rect rect)
        {
            var pen = new Pen(Brush("#cf222e"), 1);
            var geometry = new StreamGeometry();
            using var context = geometry.Open();
            var startX = rect.X;
            var y = rect.Bottom - 1;
            var step = 4d;
            var up = true;

            context.BeginFigure(new Point(startX, y), false);
            for (var x = startX + step; x <= rect.Right; x += step)
            {
                context.LineTo(new Point(x, y + (up ? -2 : 0)));
                up = !up;
            }

            drawingContext.DrawGeometry(null, pen, geometry);
        }

        private sealed record Segment(int Offset, int Length) : ISegment
        {
            public int EndOffset => Offset + Length;
        }
    }
}
