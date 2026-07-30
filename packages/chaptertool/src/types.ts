/** Severity level and message produced by the .NET Core during processing. */
export interface ChapterDiagnostic {
  /** Severity category: {@code "Information"}, {@code "Warning"}, or {@code "Error"}. */
  severity: string;
  /** Diagnostic code identifier, e.g. {@code "TimecodeText.Invalid"}. */
  code: string;
  /** Human-readable description of the issue. */
  message: string;
  /** Source location (file path or section name) when available. */
  location: string | null;
  /** Additional detail text, or {@code null} when no extra context is available. */
  details: string | null;
}

/** A single chapter entry with its timing, display name, and frame metadata. */
export interface Chapter {
  /** Chapter number as displayed to the user (1-based). */
  displayNumber: number;
  /** Start time in fractional seconds. */
  startTimeSeconds: number;
  /** Chapter display name / title text. */
  name: string;
  /** Frame-accurate representation of the start time as a human-readable string. */
  framesInfo: string;
  /** End time in fractional seconds. {@code null} for the final chapter. */
  endTimeSeconds: number | null;
  /** Describes how precisely the start time aligns to frame boundaries. */
  frameAccuracy: string;
  /** Chapter kind: {@code "Marker"} for standard chapters, or a format-specific label. */
  kind: string;
}

/**
 * A self-contained collection of chapters with source and timing metadata.
 * Returned by import, editing, and transform operations.
 */
export interface ChapterSet {
  /** Title of this chapter set, typically derived from the source file name. */
  title: string;
  /** Original source file name, or {@code null} for generated / anonymous sets. */
  sourceName: string | null;
  /** Code of the import format that produced this set. */
  importFormat: string;
  /** Frame rate used for frame-accurate calculations. */
  framesPerSecond: number;
  /** Total duration of the chapter timeline in seconds. */
  durationSeconds: number;
  /** Ordered array of chapters in this set. */
  chapters: Chapter[];
}

/**
 * One item within a {@link ChapterImportGroup}, representing a single parsed
 * source (e.g. a playlist item or a track).
 */
export interface ChapterImportEntry {
  /** Opaque identifier assigned by the importer. */
  id: string;
  /** Label shown to the user for this entry. */
  displayName: string;
  /** The chapter set extracted from this entry. */
  chapterSet: ChapterSet;
  /** Whether this entry can be combined with others into a single track. */
  canCombine: boolean;
  /** Media files referenced by the source, or {@code null} when not applicable. */
  referencedMediaFiles: ReferencedMediaFile[] | null;
}

/** A group of chapter entries belonging to the same source container (file or disc). */
export interface ChapterImportGroup {
  /** Path or identifier of the source container. */
  sourcePath: string;
  /** Entries parsed from this source. */
  entries: ChapterImportEntry[];
  /** Index of the entry that should be selected by default in a UI. */
  defaultEntryIndex: number;
}

/** A media file referenced by a chapter source (e.g. an M2TS clip inside an MPLS). */
export interface ReferencedMediaFile {
  /** File name shown to the user. */
  displayName: string;
  /** Path relative to the source container root. */
  relativePath: string;
  /** Resolved absolute path on disk, or {@code null} when unavailable. */
  absolutePath: string | null;
}

/** Top-level result returned by {@code ChapterTool.import}. */
export interface ChapterImportResult {
  /** Whether the import completed without fatal errors. */
  success: boolean;
  /** Whether only a subset of the input could be parsed. */
  isPartial: boolean;
  /** Groups produced from the input source. */
  groups: ChapterImportGroup[];
  /** Diagnostics collected during import. */
  diagnostics: ChapterDiagnostic[];
}

/** Result returned by {@code ChapterTool.export}. */
export interface ChapterExportResult {
  /** Whether the export completed without fatal errors. */
  success: boolean;
  /** Serialised chapter text / data. */
  content: string;
  /** File extension for the exported format (including the leading dot). */
  fileExtension: string;
  /** Diagnostics collected during export. */
  diagnostics: ChapterDiagnostic[];
}

/** Options controlling chapter export behaviour and output formatting. */
export interface ChapterExportOptions {
  /** Export format code, e.g. {@code "xml"}, {@code "txt"}, {@code "vtt"}. */
  format: string;
  /** XML language code for XML-based formats (ISO 639-1 or IETF BCP 47). */
  xmlLanguage?: string;
  /** Override the source file name embedded in the exported content. */
  sourceFileName?: string;
  /** When {@code true}, auto-generate chapter names if the source has none. */
  autoGenerateNames?: boolean;
  /** When {@code true}, use chapter name templates instead of source names. */
  useTemplateNames?: boolean;
  /** Template text for generating chapter names (one name per line). */
  chapterNameTemplateText?: string;
  /** Integer shift applied to chapter display numbers before export. */
  orderShift?: number;
  /** When {@code true}, apply the configured expression before export. */
  applyExpression?: boolean;
  /** Expression script text applied to chapter start times. */
  expression?: string;
  /** Pre-configured expression preset identifier. */
  expressionPresetId?: string;
  /** Source name used inside expression evaluation. */
  expressionSourceName?: string;
  /** Text encoding for the exported output. */
  textEncoding?: "Utf8" | "Utf16LittleEndian" | "Utf16BigEndian" | "Utf32LittleEndian" | "Utf32BigEndian";
  /** Whether to emit a byte-order mark (BOM) at the beginning of the output. */
  emitBom?: boolean;
  /** When {@code true}, run chapter projection prior to export. */
  projectOutput?: boolean;
}

/** Descriptor for one supported export format. */
export interface ChapterExportFormat {
  /** Stable sort index used by the .NET Core. */
  index: number;
  /** Unique format code, e.g. {@code "xml"} or {@code "qpf"}. */
  code: string;
  /** Human-readable format name. */
  displayName: string;
  /** Default file extension for this format (including the leading dot). */
  extension: string;
  /** Short description of the format and its typical use. */
  description: string;
}

/** Descriptor for one supported import format. */
export interface ChapterImportFormat {
  /** Unique format code, e.g. {@code "hddvd-xpl"}. */
  code: string;
  /** Human-readable format name. */
  displayName: string;
}

/** Result returned by edit operations (rename, delete, insert, etc.). */
export interface ChapterEditResult {
  /** The chapter set after the edit was applied. */
  chapterSet: ChapterSet;
  /** Diagnostics produced during the edit. */
  diagnostics: ChapterDiagnostic[];
}

/** Zones / keyframe list generated from selected chapters. */
export interface ChapterZonesResult {
  /** Zone list text (one zone or keyframe per line). */
  zones: string;
  /** Diagnostics collected during zone generation. */
  diagnostics: ChapterDiagnostic[];
}

/** A supported frame-rate preset. */
export interface FrameRateOption {
  /** Unique code, e.g. {@code "Fps23_976"} or {@code "Fps24"}. */
  code: string;
  /** Human-readable label for the frame rate. */
  displayName: string;
  /** Exact frame-per-second value (may be fractional for drop-frame rates). */
  value: number;
  /** Whether this frame rate is considered valid for processing. */
  isValid: boolean;
  /** Legacy MPLS code used for compatibility with older tools. */
  legacyMplsCode: number;
}

/** Result of automatic frame-rate detection on a chapter set. */
export interface FrameRateDetectionResult {
  /** The best-matching frame-rate option detected. */
  option: FrameRateOption;
  /** How many chapters matched the detected rate with high accuracy. */
  accurateChapterCount: number;
  /** Total number of chapters evaluated. */
  evaluatedChapterCount: number;
  /** Sum of timing deviations (lower is better). */
  cumulativeDeviation: number;
  /** Confidence level label, e.g. {@code "High"} or {@code "Low"}. */
  confidence: string;
}

/** Result of updating frame metadata for a chapter set. */
export interface FrameInfoResult {
  /** The chapter set after frame metadata was computed. */
  chapterSet: ChapterSet;
  /** Chapter list with updated frame information. */
  chapters: Chapter[];
  /** The frame-rate option that was selected for the computation. */
  selectedOption: FrameRateOption;
  /** Effective frame rate used. */
  framesPerSecond: number;
  /** Frame-accuracy label for each chapter. */
  accuracy: string[];
}

/** Result of a chapter transform operation (e.g. frame-rate conversion). */
export interface ChapterTransformResult {
  /** Whether the transform completed without fatal errors. */
  success: boolean;
  /** The chapter set after transformation. */
  chapterSet: ChapterSet;
  /** Diagnostics collected during the transform. */
  diagnostics: ChapterDiagnostic[];
}

/** Result of projecting chapter names and timing before export. */
export interface ChapterProjectionResult {
  /** The original chapter set (unchanged). */
  chapterSet: ChapterSet;
  /** Chapters as they will appear in the exported output. */
  outputChapters: Chapter[];
  /** Diagnostics collected during projection. */
  diagnostics: ChapterDiagnostic[];
}

/** A classified span of text within an expression script. */
export interface ExpressionTokenSpan {
  /** Zero-based start offset in the expression text. */
  start: number;
  /** Length of the span in characters. */
  length: number;
  /** Raw text content of this span. */
  text: string;
  /** Classification kind, e.g. {@code "Identifier"}, {@code "Number"}, {@code "Operator"}. */
  kind: string;
}

/** A completion suggestion for the expression editor at a specific caret position. */
export interface ExpressionCompletion {
  /** Display text shown in the completion list. */
  text: string;
  /** Symbol kind, e.g. {@code "Function"} or {@code "Variable"}. */
  kind: string;
  /** Human-readable label for the symbol kind. */
  kindLabel: string;
  /** Description of the symbol's purpose and usage. */
  description: string;
  /** Start offset in the expression where the replacement begins. */
  replacementStart: number;
  /** Number of characters to replace. */
  replacementLength: number;
  /** Text to insert when the completion is accepted. */
  insertText: string;
}

/** A diagnostic anchored to a specific span within an expression script. */
export interface ExpressionDiagnostic {
  /** The underlying chapter diagnostic. */
  diagnostic: ChapterDiagnostic;
  /** Suggested fix for this diagnostic, if available. */
  suggestion: { code: string; message: string };
  /** Zero-based start offset of the problematic span. */
  start: number;
  /** Length of the problematic span in characters. */
  length: number;
}

/** Complete result of analysing an expression script. */
export interface ExpressionAnalysisResult {
  /** Token spans identified in the expression. */
  spans: ExpressionTokenSpan[];
  /** Completion suggestions for the current caret position. */
  completions: ExpressionCompletion[];
  /** Diagnostics for errors or warnings in the expression. */
  diagnostics: ExpressionDiagnostic[];
}

/** A symbol (variable, function, or operator) available in the expression editor. */
export interface ExpressionSymbol {
  /** Symbol text as it appears in expressions. */
  text: string;
  /** Classification kind, e.g. {@code "Function"}, {@code "Variable"}, {@code "Operator"}. */
  kind: string;
  /** Human-readable description of the symbol. */
  description: string;
  /** Function arity (expected argument count), or {@code null} for non-function symbols. */
  arity: number | null;
  /** Snippet text inserted when the symbol is selected from the completion list. */
  insertText: string;
}

/** A built-in expression preset available in the expression editor. */
export interface ExpressionPreset {
  /** Unique preset identifier. */
  id: string;
  /** Name shown in the preset picker. */
  displayName: string;
  /** Short description of what the preset does. */
  description: string;
  /** Expression script text for this preset. */
  scriptText: string;
}

/** Result of parsing a time text string into seconds. */
export interface TimeParseResult {
  /** Parsed time value in fractional seconds. */
  seconds: number;
  /** Diagnostics for invalid or ambiguous input. */
  diagnostics: ChapterDiagnostic[];
}

/** Result of converting a chapter set to another text format. */
export interface ChapterConversionResult {
  /** Whether the conversion completed without fatal errors. */
  success: boolean;
  /** Converted content text. */
  content: string;
  /** File extension for the converted format (including the leading dot). */
  extension: string;
  /** Diagnostics collected during conversion. */
  diagnostics: ChapterDiagnostic[];
}

/** An XML language option available for XML-based export formats. */
export interface XmlLanguage {
  /** Language code (ISO 639-1 or IETF BCP 47). */
  code: string;
  /** Human-readable language name. */
  displayName: string;
}

/** A text-encoding option available for chapter output. */
export interface OutputEncoding {
  /** Encoding identifier used by the .NET Core. */
  id: string;
  /** Human-readable encoding name. */
  displayName: string;
  /** XML declaration encoding name (e.g. {@code "UTF-8"}). */
  xmlName: string;
}

/** UTF-8 text or raw chapter bytes accepted by the import API. */
export type ChapterInput = string | Uint8Array;
