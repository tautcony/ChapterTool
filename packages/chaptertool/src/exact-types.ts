import type {
  Chapter, ChapterDiagnostic, ChapterExportOptions, ChapterImportEntry, FrameRateOption,
  ChapterImportResult, ChapterImportGroup,
} from "./types.js";

/** Canonical authority. Integer components and decimals never pass through JS numbers. */
export type ExactFrameRate =
  | { kind: "rational"; numerator: string; denominator: string }
  | { kind: "approximate"; value: string }
  | { kind: "unknown" };

/** Signed Int64 ticks. Null ends represent absent ends. */
export interface ExactChapter extends Omit<Chapter, "startTimeSeconds" | "endTimeSeconds"> {
  startTicks: string;
  endTicks: string | null;
}

export interface ExactSegment {
  id: string;
  name: string;
  startTicks: string;
  durationTicks: string | null;
  frameRate: ExactFrameRate;
  sourceFrameRate: ExactFrameRate;
}

/** Null duration means Unknown. "0" means a known zero duration. */
export interface ExactChapterSet {
  title: string;
  sourceName: string | null;
  importFormat: string;
  frameRate: ExactFrameRate;
  durationTicks: string | null;
  chapters: ExactChapter[];
  segments?: ExactSegment[] | null;
}

export interface ExactImportEntry extends Omit<ChapterImportEntry, "chapterSet"> {
  chapterSet: ExactChapterSet;
}

export interface ExactImportGroup extends Omit<ChapterImportGroup, "entries"> {
  entries: ExactImportEntry[];
}

export interface ExactImportResult extends Omit<ChapterImportResult, "groups"> {
  groups: ExactImportGroup[];
}

/** Parse failures have no content. Operation failures preserve the input where available. */
export interface ExactContentResult {
  success: boolean;
  chapterSet: ExactChapterSet | null;
  diagnostics: ChapterDiagnostic[];
}

export interface ExactFrameRateOption extends Omit<FrameRateOption, "value"> {
  rate: ExactFrameRate;
}

export interface ExactFrameInfoResult extends ExactContentResult {
  selectedOption: ExactFrameRateOption | null;
  accuracy: string[];
}

export interface ExactDetectionResult {
  success: boolean;
  option: ExactFrameRateOption | null;
  accurateChapterCount: number;
  evaluatedChapterCount: number;
  cumulativeDeviation: string;
  confidence: string;
  diagnostics: ChapterDiagnostic[];
}

export interface ExactOutputOptions extends ChapterExportOptions {
  /** Per-call override. Content and source metadata remain unchanged. */
  computationRate?: ExactFrameRate;
}

export interface ExactProjectionResult extends ExactContentResult {
  outputChapters: ExactChapter[];
}
