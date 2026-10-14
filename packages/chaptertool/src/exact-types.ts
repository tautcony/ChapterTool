import type { ChapterDiagnostic } from "./types.js";

/** Canonical authority. Integer components and decimals never pass through JS numbers. */
export type ExactFrameRate =
  | { kind: "rational"; numerator: string; denominator: string }
  | { kind: "approximate"; value: string }
  | { kind: "unknown" };

/** Signed Int64 ticks. Null ends represent absent ends. */
export interface ExactChapter {
  displayNumber: number;
  startTicks: string;
  endTicks: string | null;
  name: string;
  framesInfo: string;
  frameAccuracy: string;
  kind: string;
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

export interface ExactImportEntry {
  id: string;
  displayName: string;
  chapterSet: ExactChapterSet;
  canCombine: boolean;
  referencedMediaFiles: { displayName: string; relativePath: string; absolutePath: string | null }[] | null;
  mediaTracks?: { kind: string; summary: string; codec: string | null; format: string | null; language: string | null;
    channels: string | null; sampleRate: string | null; aspectRatio: string | null }[] | null;
}

export interface ExactImportGroup {
  sourcePath: string;
  entries: ExactImportEntry[];
  defaultEntryIndex: number;
}

export interface ExactImportResult {
  success: boolean;
  isPartial: boolean;
  groups: ExactImportGroup[];
  diagnostics: ChapterDiagnostic[];
}

/** Parse failures have no content. Operation failures preserve the input where available. */
export interface ExactContentResult {
  success: boolean;
  chapterSet: ExactChapterSet | null;
  diagnostics: ChapterDiagnostic[];
}

export interface ExactFrameRateOption {
  code: string;
  displayName: string;
  isValid: boolean;
  mplsFrameRateCode: number;
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

export interface ExactOutputOptions {
  format: string;
  xmlLanguage?: string;
  sourceFileName?: string;
  textEncoding?: "Utf8" | "Utf16LittleEndian" | "Utf16BigEndian" | "Utf32LittleEndian" | "Utf32BigEndian";
  /** Rate for formats that serialize frame numbers. */
  computationRate?: ExactFrameRate;
}
