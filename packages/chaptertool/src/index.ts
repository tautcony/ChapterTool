import { dotnet } from "./runtime/_framework/dotnet.js";
import { createRetryableLoader } from "./api-loader.js";
import { requireFileName, toBytes } from "./utils/input.js";
import { encodeJson, invokeJson } from "./utils/json.js";
import {
  requireBoolean,
  requireIndex,
  requireIndexes,
  requireInteger,
  requireNumber,
  requireObject,
  requireString
} from "./utils/validation.js";
import type {
  ChapterConversionResult,
  ChapterEditResult,
  ChapterExportFormat,
  ChapterExportOptions,
  ChapterExportResult,
  ChapterImportFormat,
  ChapterImportGroup,
  ChapterImportResult,
  ChapterInput,
  ChapterProjectionResult,
  ChapterSet,
  ChapterTransformResult,
  ChapterZonesResult,
  ExpressionAnalysisResult,
  ExpressionPreset,
  ExpressionSymbol,
  FrameInfoResult,
  FrameRateDetectionResult,
  FrameRateOption,
  OutputEncoding,
  TimeParseResult,
  XmlLanguage
} from "./types.js";

export type * from "./types.js";

interface NodeApi {
  [operation: string]: (...arguments_: unknown[]) => unknown;
}

interface ChapterToolAssembly {
  ChapterTool: {
    Node: {
      NodeApi: NodeApi;
    };
  };
}

/**
 * Node.js ChapterTool API backed by the .NET WebAssembly Core.
 * Runtime initialization is lazy, shared by all instances, and retryable.
 */
export class ChapterTool {
  static readonly #loadApi = createRetryableLoader(async (): Promise<NodeApi> => {
    const runtime = await dotnet
      .withMainAssembly("ChapterTool.Node")
      .create();
    const assembly = await runtime.getAssemblyExports<ChapterToolAssembly>("ChapterTool.Node");
    return assembly.ChapterTool.Node.NodeApi;
  });

  async #invoke<T>(operation: string, ...arguments_: unknown[]): Promise<T> {
    const api = await ChapterTool.#loadApi();
    return api[operation](...arguments_) as T;
  }

  /**
   * The .NET exports use JSON strings for complex values because JSExport does
   * not marshal the ChapterTool records as JavaScript objects.
   */
  async #invokeJson<T>(operation: string, ...arguments_: unknown[]): Promise<T> {
    const api = await ChapterTool.#loadApi();
    return invokeJson<T>(api, operation, ...arguments_);
  }

  /** Imports text or bytes and returns chapter groups with diagnostics. */
  async import(content: ChapterInput, options: { fileName?: string } = {}): Promise<ChapterImportResult> {
    const fileName = requireFileName(requireObject(options, "options").fileName);
    const bytes = toBytes(content);
    return this.#invokeJson<ChapterImportResult>("Import", fileName, bytes.toString("base64"));
  }

  /** Exports one chapter set with the selected format and export options. */
  async export(chapterSet: ChapterSet, options: ChapterExportOptions): Promise<ChapterExportResult> {
    requireObject(chapterSet, "chapterSet");

    const normalizedOptions = requireObject(options, "options");
    if (typeof normalizedOptions.format !== "string" || normalizedOptions.format.length === 0) {
      throw new TypeError("options.format must be an export format code, such as 'xml'.");
    }

    return this.#invokeJson<ChapterExportResult>(
      "Export",
      encodeJson(chapterSet, "chapterSet"),
      encodeJson(normalizedOptions, "options"));
  }

  /** Returns the supported chapter export formats. */
  async formats(): Promise<ChapterExportFormat[]> {
    return this.#invokeJson<ChapterExportFormat[]>("GetFormats");
  }

  /** Returns the supported chapter import formats. */
  async importFormats(): Promise<ChapterImportFormat[]> {
    return this.#invokeJson<ChapterImportFormat[]>("GetImportFormats");
  }

  /** Checks whether a file extension uses a binary chapter importer. */
  async isBinaryExtension(extension: string): Promise<boolean> {
    return this.#invoke<boolean>("IsBinaryExtension", requireString(extension, "extension"));
  }

  /** Replaces one chapter start time with parsed time text. */
  async editTime(chapterSet: ChapterSet, index: number, text: string): Promise<ChapterEditResult> {
    return this.#invokeJson<ChapterEditResult>(
      "Edit",
      encodeJson(requireObject(chapterSet, "chapterSet"), "chapterSet"),
      "editTime",
      encodeJson({ index: requireIndex(index, "index"), text: requireString(text, "text") }, "options"));
  }

  /** Replaces one chapter start time with a frame number. */
  async editFrame(chapterSet: ChapterSet, index: number, text: string, framesPerSecond: number): Promise<ChapterEditResult> {
    return this.#invokeJson<ChapterEditResult>(
      "Edit",
      encodeJson(requireObject(chapterSet, "chapterSet"), "chapterSet"),
      "editFrame",
      encodeJson({
        index: requireIndex(index, "index"),
        text: requireString(text, "text"),
        framesPerSecond: requireNumber(framesPerSecond, "framesPerSecond")
      }, "options"));
  }

  /** Changes one chapter name. */
  async rename(chapterSet: ChapterSet, index: number, name: string): Promise<ChapterEditResult> {
    return this.#invokeJson<ChapterEditResult>(
      "Edit",
      encodeJson(requireObject(chapterSet, "chapterSet"), "chapterSet"),
      "rename",
      encodeJson({ index: requireIndex(index, "index"), text: requireString(name, "name") }, "options"));
  }

  /** Deletes chapters at the specified zero-based indexes. */
  async delete(chapterSet: ChapterSet, indexes: number[]): Promise<ChapterEditResult> {
    return this.#invokeJson<ChapterEditResult>(
      "Edit",
      encodeJson(requireObject(chapterSet, "chapterSet"), "chapterSet"),
      "delete",
      encodeJson({ indexes: requireIndexes(indexes) }, "options"));
  }

  /** Inserts a new chapter before the specified zero-based index. */
  async insertBefore(chapterSet: ChapterSet, index: number): Promise<ChapterEditResult> {
    return this.#invokeJson<ChapterEditResult>(
      "Edit",
      encodeJson(requireObject(chapterSet, "chapterSet"), "chapterSet"),
      "insertBefore",
      encodeJson({ index: requireIndex(index, "index") }, "options"));
  }

  /** Shifts chapter display numbers by the specified integer. */
  async applyOrderShift(chapterSet: ChapterSet, shift: number): Promise<ChapterEditResult> {
    return this.#invokeJson<ChapterEditResult>(
      "Edit",
      encodeJson(requireObject(chapterSet, "chapterSet"), "chapterSet"),
      "applyOrderShift",
      encodeJson({ shift: requireInteger(shift, "shift") }, "options"));
  }

  /** Applies one chapter name per line from a template text. */
  async applyTemplate(chapterSet: ChapterSet, templateText: string): Promise<ChapterEditResult> {
    return this.#invokeJson<ChapterEditResult>(
      "Edit",
      encodeJson(requireObject(chapterSet, "chapterSet"), "chapterSet"),
      "applyTemplate",
      encodeJson({ text: requireString(templateText, "templateText") }, "options"));
  }

  /** Moves chapter start times forward by a frame count. */
  async shiftFramesForward(chapterSet: ChapterSet, frames: number, framesPerSecond: number): Promise<ChapterEditResult> {
    return this.#invokeJson<ChapterEditResult>(
      "Edit",
      encodeJson(requireObject(chapterSet, "chapterSet"), "chapterSet"),
      "shiftFramesForward",
      encodeJson({
        frames: requireInteger(frames, "frames"),
        framesPerSecond: requireNumber(framesPerSecond, "framesPerSecond")
      }, "options"));
  }

  /** Creates a zones or keyframe list for selected chapters. */
  async createZones(chapterSet: ChapterSet, indexes: number[], framesPerSecond: number): Promise<ChapterZonesResult> {
    return this.#invokeJson<ChapterZonesResult>(
      "CreateZones",
      encodeJson(requireObject(chapterSet, "chapterSet"), "chapterSet"),
      encodeJson(requireIndexes(indexes), "indexes"),
      requireNumber(framesPerSecond, "framesPerSecond"));
  }

  /** Combines the entries in one imported source group. */
  async combine(source: ChapterImportGroup): Promise<ChapterEditResult> {
    return this.#invokeJson<ChapterEditResult>(
      "Combine",
      encodeJson(requireObject(source, "source"), "source"));
  }

  /** Appends one imported MPLS source group to another. */
  async append(existing: ChapterImportGroup, appended: ChapterImportGroup): Promise<ChapterEditResult> {
    return this.#invokeJson<ChapterEditResult>(
      "Append",
      encodeJson(requireObject(existing, "existing"), "existing"),
      encodeJson(requireObject(appended, "appended"), "appended"));
  }

  /** Returns the supported frame-rate options. */
  async frameRates(): Promise<FrameRateOption[]> {
    return this.#invokeJson<FrameRateOption[]>("GetFrameRates");
  }

  /** Finds the closest supported frame-rate option. */
  async findFrameRate(framesPerSecond: number): Promise<FrameRateOption> {
    return this.#invokeJson<FrameRateOption>(
      "FindFrameRate",
      requireNumber(framesPerSecond, "framesPerSecond"));
  }

  /** Detects the most likely frame rate from chapter timing data. */
  async detectFrameRate(chapterSet: ChapterSet, tolerance = 0.15): Promise<FrameRateDetectionResult> {
    return this.#invokeJson<FrameRateDetectionResult>(
      "DetectFrameRate",
      encodeJson(requireObject(chapterSet, "chapterSet"), "chapterSet"),
      requireNumber(tolerance, "tolerance"));
  }

  /** Calculates chapter frame metadata for the selected frame-rate option. */
  async updateFrames(
    chapterSet: ChapterSet,
    options: { optionCode?: string; round?: boolean; tolerance?: number } = {}
  ): Promise<FrameInfoResult> {
    const normalizedOptions = requireObject(options, "options");
    const optionCode = normalizedOptions.optionCode ?? "Auto";
    const round = normalizedOptions.round ?? true;
    if (typeof optionCode !== "string") {
      throw new TypeError("options.optionCode must be a string.");
    }
    requireBoolean(round, "options.round");

    return this.#invokeJson<FrameInfoResult>(
      "UpdateFrames",
      encodeJson(requireObject(chapterSet, "chapterSet"), "chapterSet"),
      optionCode,
      round,
      requireNumber(normalizedOptions.tolerance ?? 0.15, "options.tolerance"));
  }

  /** Converts chapter timing from one frame rate to another. */
  async changeFrameRate(chapterSet: ChapterSet, sourceFps: number, targetFps: number): Promise<ChapterTransformResult> {
    return this.#invokeJson<ChapterTransformResult>(
      "ChangeFrameRate",
      encodeJson(requireObject(chapterSet, "chapterSet"), "chapterSet"),
      requireNumber(sourceFps, "sourceFps"),
      requireNumber(targetFps, "targetFps"));
  }

  /** Applies a time expression to chapter start times. */
  async applyExpression(chapterSet: ChapterSet, expression: string, enabled = true): Promise<ChapterEditResult> {
    return this.#invokeJson<ChapterEditResult>(
      "ApplyExpression",
      encodeJson(requireObject(chapterSet, "chapterSet"), "chapterSet"),
      requireBoolean(enabled, "enabled"),
      requireString(expression, "expression"));
  }

  /** Projects chapter names and timing before export. */
  async project(chapterSet: ChapterSet, options: ChapterExportOptions): Promise<ChapterProjectionResult> {
    const normalizedOptions = requireObject(options, "options");
    if (typeof normalizedOptions.format !== "string" || normalizedOptions.format.length === 0) {
      throw new TypeError("options.format must be an export format code, such as 'xml'.");
    }

    return this.#invokeJson<ChapterProjectionResult>(
      "Project",
      encodeJson(requireObject(chapterSet, "chapterSet"), "chapterSet"),
      encodeJson(normalizedOptions, "options"));
  }

  /** Analyzes an expression and returns spans, completions, and diagnostics. */
  async analyzeExpression(
    expression: string,
    options: { caretIndex?: number; timeSeconds?: number; framesPerSecond?: number } = {}
  ): Promise<ExpressionAnalysisResult> {
    const normalizedExpression = requireString(expression, "expression");
    const normalizedOptions = requireObject(options, "options");
    return this.#invokeJson<ExpressionAnalysisResult>(
      "AnalyzeExpression",
      normalizedExpression,
      requireInteger(normalizedOptions.caretIndex ?? normalizedExpression.length, "options.caretIndex"),
      requireNumber(normalizedOptions.timeSeconds ?? 0, "options.timeSeconds"),
      requireNumber(normalizedOptions.framesPerSecond ?? 24, "options.framesPerSecond"));
  }

  /** Returns the symbols available to the expression editor. */
  async expressionSymbols(): Promise<ExpressionSymbol[]> {
    return this.#invokeJson<ExpressionSymbol[]>("GetExpressionSymbols");
  }

  /** Returns the built-in expression presets. */
  async expressionPresets(): Promise<ExpressionPreset[]> {
    return this.#invokeJson<ExpressionPreset[]>("GetExpressionPresets");
  }

  /** Parses time text and returns diagnostics for invalid input. */
  async parseTime(text: string): Promise<TimeParseResult> {
    return this.#invokeJson<TimeParseResult>("ParseTime", requireString(text, "text"));
  }

  /** Parses time text and returns zero when parsing fails. */
  async parseTimeOrZero(text: string): Promise<number> {
    return this.#invoke<number>("ParseTimeOrZero", requireString(text, "text"));
  }

  /** Formats seconds as chapter time text. */
  async formatTime(seconds: number): Promise<string> {
    return this.#invoke<string>("FormatTime", requireNumber(seconds, "seconds"));
  }

  /** Formats seconds as cue-sheet time text. */
  async formatCueTime(seconds: number): Promise<string> {
    return this.#invoke<string>("FormatCueTime", requireNumber(seconds, "seconds"));
  }

  /** Converts a chapter set to Celltimes content. */
  async toCelltimes(chapterSet: ChapterSet, framesPerSecond: number): Promise<ChapterConversionResult> {
    return this.#invokeJson<ChapterConversionResult>(
      "ToCelltimes",
      encodeJson(requireObject(chapterSet, "chapterSet"), "chapterSet"),
      requireNumber(framesPerSecond, "framesPerSecond"));
  }

  /** Converts chapter text to QPFile content. */
  async chapterTextToQpfile(
    chapterText: string,
    framesPerSecond: number,
    timecodeText: string | null = null
  ): Promise<ChapterConversionResult> {
    if (timecodeText !== null && timecodeText !== undefined) {
      requireString(timecodeText, "timecodeText");
    }

    return this.#invokeJson<ChapterConversionResult>(
      "ChapterTextToQpfile",
      requireString(chapterText, "chapterText"),
      requireNumber(framesPerSecond, "framesPerSecond"),
      timecodeText);
  }

  /** Returns the XML language options. */
  async xmlLanguages(): Promise<XmlLanguage[]> {
    return this.#invokeJson<XmlLanguage[]>("GetXmlLanguages");
  }

  /** Returns the supported output encodings. */
  async outputEncodings(): Promise<OutputEncoding[]> {
    return this.#invokeJson<OutputEncoding[]>("GetOutputEncodings");
  }
}
