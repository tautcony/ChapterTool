export interface ChapterDiagnostic {
  severity: string;
  code: string;
  message: string;
  location: string | null;
  details: string | null;
}

export interface Chapter {
  displayNumber: number;
  startTimeSeconds: number;
  name: string;
  framesInfo: string;
  endTimeSeconds: number | null;
  frameAccuracy: string;
  kind: string;
}

export interface ChapterSet {
  title: string;
  sourceName: string | null;
  importFormat: string;
  framesPerSecond: number;
  durationSeconds: number;
  chapters: Chapter[];
}

export interface ChapterImportEntry {
  id: string;
  displayName: string;
  chapterSet: ChapterSet;
  canCombine: boolean;
  referencedMediaFiles: ReferencedMediaFile[] | null;
}

export interface ChapterImportGroup {
  sourcePath: string;
  entries: ChapterImportEntry[];
  defaultEntryIndex: number;
}

export interface ReferencedMediaFile {
  displayName: string;
  relativePath: string;
  absolutePath: string | null;
}

export interface ChapterImportResult {
  success: boolean;
  isPartial: boolean;
  groups: ChapterImportGroup[];
  diagnostics: ChapterDiagnostic[];
}

export interface ChapterExportResult {
  success: boolean;
  content: string;
  fileExtension: string;
  diagnostics: ChapterDiagnostic[];
}

export interface ChapterExportOptions {
  format: string;
  xmlLanguage?: string;
  sourceFileName?: string;
  autoGenerateNames?: boolean;
  useTemplateNames?: boolean;
  chapterNameTemplateText?: string;
  orderShift?: number;
  applyExpression?: boolean;
  expression?: string;
  expressionPresetId?: string;
  expressionSourceName?: string;
  textEncoding?: "Utf8" | "Utf16LittleEndian" | "Utf16BigEndian" | "Utf32LittleEndian" | "Utf32BigEndian";
  emitBom?: boolean;
  projectOutput?: boolean;
}

export interface ChapterExportFormat {
  index: number;
  code: string;
  displayName: string;
  extension: string;
  description: string;
}

export interface ChapterImportFormat {
  code: string;
  displayName: string;
}

export interface ChapterEditResult {
  chapterSet: ChapterSet;
  diagnostics: ChapterDiagnostic[];
}

export interface ChapterZonesResult {
  zones: string;
  diagnostics: ChapterDiagnostic[];
}

export interface FrameRateOption {
  code: string;
  displayName: string;
  value: number;
  isValid: boolean;
  legacyMplsCode: number;
}

export interface FrameRateDetectionResult {
  option: FrameRateOption;
  accurateChapterCount: number;
  evaluatedChapterCount: number;
  cumulativeDeviation: number;
  confidence: string;
}

export interface FrameInfoResult {
  chapterSet: ChapterSet;
  chapters: Chapter[];
  selectedOption: FrameRateOption;
  framesPerSecond: number;
  accuracy: string[];
}

export interface ChapterTransformResult {
  success: boolean;
  chapterSet: ChapterSet;
  diagnostics: ChapterDiagnostic[];
}

export interface ChapterProjectionResult {
  chapterSet: ChapterSet;
  outputChapters: Chapter[];
  diagnostics: ChapterDiagnostic[];
}

export interface ExpressionTokenSpan {
  start: number;
  length: number;
  text: string;
  kind: string;
}

export interface ExpressionCompletion {
  text: string;
  kind: string;
  kindLabel: string;
  description: string;
  replacementStart: number;
  replacementLength: number;
  insertText: string;
}

export interface ExpressionDiagnostic {
  diagnostic: ChapterDiagnostic;
  suggestion: { code: string; message: string };
  start: number;
  length: number;
}

export interface ExpressionAnalysisResult {
  spans: ExpressionTokenSpan[];
  completions: ExpressionCompletion[];
  diagnostics: ExpressionDiagnostic[];
}

export interface ExpressionSymbol {
  text: string;
  kind: string;
  description: string;
  arity: number | null;
  insertText: string;
}

export interface ExpressionPreset {
  id: string;
  displayName: string;
  description: string;
  scriptText: string;
}

export interface TimeParseResult {
  seconds: number;
  diagnostics: ChapterDiagnostic[];
}

export interface ChapterConversionResult {
  success: boolean;
  content: string;
  extension: string;
  diagnostics: ChapterDiagnostic[];
}

export interface XmlLanguage {
  code: string;
  displayName: string;
}

export interface OutputEncoding {
  id: string;
  displayName: string;
  xmlName: string;
}

/** UTF-8 text or raw chapter bytes accepted by the import API. */
export type ChapterInput = string | Uint8Array;
