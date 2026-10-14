/** Severity and message produced by ChapterTool Core. */
export interface ChapterDiagnostic {
  severity: string;
  code: string;
  message: string;
  location: string | null;
  details: string | null;
}

/** Result returned by serialization. */
export interface ChapterExportResult {
  success: boolean;
  content: string;
  fileExtension: string;
  diagnostics: ChapterDiagnostic[];
}

/** Result returned by a zone conversion. */
export interface ChapterZonesResult {
  zones: string;
  diagnostics: ChapterDiagnostic[];
}

/** Result returned by a text conversion. */
export interface ChapterConversionResult {
  success: boolean;
  content: string;
  extension: string;
  diagnostics: ChapterDiagnostic[];
}

/** UTF-8 text or raw chapter bytes accepted by the import API. */
export type ChapterInput = string | Uint8Array;

/** Export format metadata from Core. */
export interface ChapterFormat {
  index: number;
  code: string;
  displayName: string;
  extension: string;
  description: string;
}

/** Import format metadata from Core. */
export interface ChapterImportFormat {
  code: string;
  displayName: string;
}

/** XML chapter language metadata. */
export interface ChapterXmlLanguage {
  code: string;
  displayName: string;
}

/** Supported output encoding metadata. */
export interface ChapterOutputEncoding {
  id: string;
  displayName: string;
  xmlName: string;
}
