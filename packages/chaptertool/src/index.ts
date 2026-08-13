import { dotnet } from "./runtime/_framework/dotnet.js";
import { createRetryableLoader } from "./api-loader.js";
import { applyMaxInputBytes, requireFileName, toBytes } from "./utils/input.js";
import { encodeJson, invokeJson } from "./utils/json.js";
import {
  requireBoolean,
  requireIndex,
  requireIndexes,
  requireInteger,
  requireNumber,
  requireObject,
  requireString,
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
  XmlLanguage,
} from "./types.js";

export type * from "./types.js";
export { MAX_INPUT_BYTES } from "./utils/input.js";

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
 * Portable ChapterTool Core API for Node.js, backed by the .NET WebAssembly
 * runtime.  The underlying WASM module is loaded lazily on first use and
 * shared across all {@link ChapterTool} instances.  When the runtime fails to
 * start (e.g. because of a transient resource limit), subsequent calls
 * automatically retry the initialisation.
 *
 * @example
 * ```ts
 * const tool = new ChapterTool();
 * const { groups } = await tool.import(
 *   "CHAPTER01=00:00:00.000\\nCHAPTER01NAME=Opening\\n",
 *   { fileName: "chapters.txt" },
 * );
 * const chapterSet = groups[0].entries[0].chapterSet;
 * const exported = await tool.export(chapterSet, { format: "xml" });
 * console.log(exported.content);
 * ```
 */
export class ChapterTool {
  /** Shared, retryable loader that resolves once the .NET runtime is ready. */
  static readonly #loadApi = createRetryableLoader(async (): Promise<NodeApi> => {
    const runtime = await dotnet
      .withMainAssembly("ChapterTool.Node")
      .create();
    const assembly = await runtime.getAssemblyExports<ChapterToolAssembly>("ChapterTool.Node");
    const api = assembly.ChapterTool.Node.NodeApi;
    applyMaxInputBytes(Number(api.GetMaxInputBytes()));
    return api;
  });

  /**
   * Invokes a named operation on the .NET API, casting the return value to
   * {@link T}.  Prefer {@link #invokeJson} for operations that marshal complex
   * data through JSON strings.
   *
   * @typeParam T - Expected return type.
   * @param operation - Method name on the .NET `NodeApi`.
   * @param arguments_ - Positional arguments forwarded to the .NET method.
   * @returns The value produced by the .NET export.
   */
  async #invoke<T>(operation: string, ...arguments_: unknown[]): Promise<T> {
    const api = await ChapterTool.#loadApi();
    return api[operation](...arguments_) as T;
  }

  /**
   * Invokes a named .NET operation that returns a JSON string, then parses it
   * into the expected type.  ChapterTool's .NET records are exposed as JSON
   * because `JSExport` does not automatically marshal them to JavaScript
   * objects.
   *
   * @typeParam T - Shape of the deserialised JSON value.
   * @param operation - Method name on the .NET `NodeApi`.
   * @param arguments_ - Positional arguments forwarded to the .NET method.
   * @returns The deserialised value.
   * @throws {Error} When the .NET method returns invalid JSON.
   */
  async #invokeJson<T>(operation: string, ...arguments_: unknown[]): Promise<T> {
    const api = await ChapterTool.#loadApi();
    return invokeJson<T>(api, operation, ...arguments_);
  }

  /**
   * Imports chapter content from a UTF-8 string, a {@link Buffer}, or a
   * `Uint8Array`, returning typed groups with per-entry diagnostics.
   *
   * Inputs larger than {@link MAX_INPUT_BYTES} are rejected before reaching the
   * WASM boundary.
   *
   * @param content - Chapter source text or raw bytes.
   * @param options  - Optional file-name hint used to select the importer.
   * @param options.fileName - Source file name including extension.  Defaults
   *   to {@code "input.txt"} when omitted.
   * @returns The parsed groups together with any diagnostics.
   * @throws {TypeError} When {@code content} is not a string, Buffer, or
   *   `Uint8Array`, or when {@code options.fileName} is an empty string.
   * @throws {RangeError} When the UTF-8 byte length exceeds
   *   {@link MAX_INPUT_BYTES}.
   */
  async import(content: ChapterInput, options: { fileName?: string } = {}): Promise<ChapterImportResult> {
    const fileName = requireFileName(requireObject(options, "options").fileName);
    await ChapterTool.#loadApi();
    const bytes = toBytes(content);
    return this.#invokeJson<ChapterImportResult>("Import", fileName, bytes.toString("base64"));
  }

  /**
   * Returns the live portable input byte limit from the .NET Core.
   */
  async maxInputBytes(): Promise<number> {
    return Number(await this.#invoke<number>("GetMaxInputBytes"));
  }

  /**
   * Exports a chapter set using the specified format and options.
   *
   * @param chapterSet - The chapter set to export (as returned by {@link import}
   *   or any edit operation).
   * @param options - Export configuration; {@code options.format} must be a
   *   non-empty format code such as {@code "xml"} or {@code "qpf"}.
   * @returns The serialised content, its file extension, and diagnostics.
   * @throws {TypeError} When {@code options.format} is missing or empty.
   */
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

  /**
   * Returns descriptors for every export format supported by the Core.
   *
   * @returns An array of {@link ChapterExportFormat} entries, each with a
   *   unique {@code code} and default file extension.
   */
  async formats(): Promise<ChapterExportFormat[]> {
    return this.#invokeJson<ChapterExportFormat[]>("GetFormats");
  }

  /**
   * Returns descriptors for every import format recognised by the Core.
   *
   * @returns An array of {@link ChapterImportFormat} entries.
   */
  async importFormats(): Promise<ChapterImportFormat[]> {
    return this.#invokeJson<ChapterImportFormat[]>("GetImportFormats");
  }

  /**
   * Reports whether a file extension maps to a binary (non-text) chapter
   * importer.
   *
   * @param extension - File extension including the leading dot
   *   (e.g. {@code ".mpls"}).
   * @returns {@code true} when the extension triggers a binary reader.
   */
  async isBinaryExtension(extension: string): Promise<boolean> {
    return this.#invoke<boolean>("IsBinaryExtension", requireString(extension, "extension"));
  }

  /**
   * Re-parses the start time of one chapter from a time-text value.
   *
   * @param chapterSet - The chapter set to modify.
   * @param index - Zero-based index of the chapter whose time will be replaced.
   * @param text - Time text in a format understood by the Core (e.g.
   *   {@code "00:05:23.500"}).
   * @returns The updated chapter set with any diagnostics.
   * @throws {RangeError} When {@code index} is negative.
   */
  async editTime(chapterSet: ChapterSet, index: number, text: string): Promise<ChapterEditResult> {
    return this.#invokeJson<ChapterEditResult>(
      "Edit",
      encodeJson(requireObject(chapterSet, "chapterSet"), "chapterSet"),
      "editTime",
      encodeJson({ index: requireIndex(index, "index"), text: requireString(text, "text") }, "options"));
  }

  /**
   * Replaces one chapter start time using a frame number and frame rate.
   *
   * @param chapterSet - The chapter set to modify.
   * @param index - Zero-based index of the chapter to edit.
   * @param text - Frame number as a string (e.g. {@code "1440"}).
   * @param framesPerSecond - Frame rate used to convert the frame number.
   * @returns The updated chapter set with any diagnostics.
   * @throws {RangeError} When {@code index} is negative.
   */
  async editFrame(chapterSet: ChapterSet, index: number, text: string, framesPerSecond: number): Promise<ChapterEditResult> {
    return this.#invokeJson<ChapterEditResult>(
      "Edit",
      encodeJson(requireObject(chapterSet, "chapterSet"), "chapterSet"),
      "editFrame",
      encodeJson({
        index: requireIndex(index, "index"),
        text: requireString(text, "text"),
        framesPerSecond: requireNumber(framesPerSecond, "framesPerSecond"),
      }, "options"));
  }

  /**
   * Changes the display name of a single chapter.
   *
   * @param chapterSet - The chapter set to modify.
   * @param index - Zero-based index of the chapter to rename.
   * @param name - New chapter name (may be empty).
   * @returns The updated chapter set with any diagnostics.
   * @throws {RangeError} When {@code index} is negative.
   */
  async rename(chapterSet: ChapterSet, index: number, name: string): Promise<ChapterEditResult> {
    return this.#invokeJson<ChapterEditResult>(
      "Edit",
      encodeJson(requireObject(chapterSet, "chapterSet"), "chapterSet"),
      "rename",
      encodeJson({ index: requireIndex(index, "index"), text: requireString(name, "name") }, "options"));
  }

  /**
   * Deletes one or more chapters by their zero-based indexes.
   *
   * @param chapterSet - The chapter set to modify.
   * @param indexes - Array of zero-based chapter indexes to remove.  Negative
   *   values are rejected.
   * @returns The updated chapter set (with remaining chapters re-indexed) and
   *   any diagnostics.
   * @throws {TypeError} When any index is not a safe, non-negative integer.
   */
  async delete(chapterSet: ChapterSet, indexes: number[]): Promise<ChapterEditResult> {
    return this.#invokeJson<ChapterEditResult>(
      "Edit",
      encodeJson(requireObject(chapterSet, "chapterSet"), "chapterSet"),
      "delete",
      encodeJson({ indexes: requireIndexes(indexes) }, "options"));
  }

  /**
   * Inserts a new, empty chapter immediately before the given index.
   *
   * @param chapterSet - The chapter set to modify.
   * @param index - Zero-based position where the new chapter will be inserted.
   *   After the operation the new chapter occupies this index.
   * @returns The expanded chapter set with any diagnostics.
   * @throws {RangeError} When {@code index} is negative.
   */
  async insertBefore(chapterSet: ChapterSet, index: number): Promise<ChapterEditResult> {
    return this.#invokeJson<ChapterEditResult>(
      "Edit",
      encodeJson(requireObject(chapterSet, "chapterSet"), "chapterSet"),
      "insertBefore",
      encodeJson({ index: requireIndex(index, "index") }, "options"));
  }

  /**
   * Adds a constant integer offset to every chapter's display number.
   *
   * @param chapterSet - The chapter set to modify.
   * @param shift - Integer value added to each chapter's {@code displayNumber}.
   *   May be negative.
   * @returns The updated chapter set with renumbered display numbers.
   */
  async applyOrderShift(chapterSet: ChapterSet, shift: number): Promise<ChapterEditResult> {
    return this.#invokeJson<ChapterEditResult>(
      "Edit",
      encodeJson(requireObject(chapterSet, "chapterSet"), "chapterSet"),
      "applyOrderShift",
      encodeJson({ shift: requireInteger(shift, "shift") }, "options"));
  }

  /**
   * Assigns chapter names from a template string where each line corresponds
   * to one chapter in order.
   *
   * @param chapterSet - The chapter set to modify.
   * @param templateText - Name template text (one name per line).  Lines after
   *   the chapter count are ignored; fewer lines leave trailing chapters
   *   unchanged.
   * @returns The updated chapter set with any diagnostics.
   */
  async applyTemplate(chapterSet: ChapterSet, templateText: string): Promise<ChapterEditResult> {
    return this.#invokeJson<ChapterEditResult>(
      "Edit",
      encodeJson(requireObject(chapterSet, "chapterSet"), "chapterSet"),
      "applyTemplate",
      encodeJson({ text: requireString(templateText, "templateText") }, "options"));
  }

  /**
   * Moves every chapter start time forward by a given number of frames.
   * When the shift is positive, the first chapter (at 0 s) is removed.
   *
   * @param chapterSet - The chapter set to modify.  Must contain at least one
   *   chapter.
   * @param frames - Number of frames to shift forward (must be a safe integer).
   * @param framesPerSecond - Frame rate used to convert the frame count to
   *   seconds.
   * @returns The updated chapter set.  When the shift is positive, the chapter
   *   that previously was at time 0 is dropped.
   */
  async shiftFramesForward(chapterSet: ChapterSet, frames: number, framesPerSecond: number): Promise<ChapterEditResult> {
    return this.#invokeJson<ChapterEditResult>(
      "Edit",
      encodeJson(requireObject(chapterSet, "chapterSet"), "chapterSet"),
      "shiftFramesForward",
      encodeJson({
        frames: requireInteger(frames, "frames"),
        framesPerSecond: requireNumber(framesPerSecond, "framesPerSecond"),
      }, "options"));
  }

  /**
   * Generates a keyframe / zones list for the selected chapters at the given
   * frame rate.
   *
   * @param chapterSet - Source chapter set.
   * @param indexes - Zero-based indexes of the chapters to include.
   * @param framesPerSecond - Frame rate for the keyframe calculation.
   * @returns Zone list text (one entry per line) with diagnostics.
   */
  async createZones(chapterSet: ChapterSet, indexes: number[], framesPerSecond: number): Promise<ChapterZonesResult> {
    return this.#invokeJson<ChapterZonesResult>(
      "CreateZones",
      encodeJson(requireObject(chapterSet, "chapterSet"), "chapterSet"),
      encodeJson(requireIndexes(indexes), "indexes"),
      requireNumber(framesPerSecond, "framesPerSecond"));
  }

  /**
   * Merges all entries within one imported source group into a single chapter
   * set (e.g. combining MPLS playlist items into one track).
   *
   * @param source - An import group typically obtained from
   *   {@link ChapterImportResult.groups}.
   * @returns A single chapter set that combines every entry in the group.
   */
  async combine(source: ChapterImportGroup): Promise<ChapterEditResult> {
    return this.#invokeJson<ChapterEditResult>(
      "Combine",
      encodeJson(requireObject(source, "source"), "source"));
  }

  /**
   * Appends the entries of one imported source group to another (e.g.
   * concatenating two MPLS playlists).
   *
   * @param existing - The base group.
   * @param appended - The group whose entries are appended.
   * @returns A single chapter set containing entries from both groups in order.
   */
  async append(existing: ChapterImportGroup, appended: ChapterImportGroup): Promise<ChapterEditResult> {
    return this.#invokeJson<ChapterEditResult>(
      "Append",
      encodeJson(requireObject(existing, "existing"), "existing"),
      encodeJson(requireObject(appended, "appended"), "appended"));
  }

  /**
   * Returns the list of frame-rate presets known to the Core.
   *
   * @returns Supported frame-rate options, each with a unique {@code code} and
   *   exact numeric value.
   */
  async frameRates(): Promise<FrameRateOption[]> {
    return this.#invokeJson<FrameRateOption[]>("GetFrameRates");
  }

  /**
   * Finds the closest supported frame-rate preset for a given numeric value.
   *
   * @param framesPerSecond - Target frames per second (e.g. 23.976).
   * @returns The nearest {@link FrameRateOption} recognised by the Core.
   */
  async findFrameRate(framesPerSecond: number): Promise<FrameRateOption> {
    return this.#invokeJson<FrameRateOption>(
      "FindFrameRate",
      requireNumber(framesPerSecond, "framesPerSecond"));
  }

  /**
   * Heuristically detects the most likely frame rate for a chapter set by
   * analysing how chapter start times align with known frame-rate grids.
   *
   * @param chapterSet - The chapter set to analyse.
   * @param tolerance - Allowed per-chapter deviation ratio (default 0.15).
   * @returns The best match together with accuracy and confidence metrics.
   */
  async detectFrameRate(chapterSet: ChapterSet, tolerance = 0.15): Promise<FrameRateDetectionResult> {
    return this.#invokeJson<FrameRateDetectionResult>(
      "DetectFrameRate",
      encodeJson(requireObject(chapterSet, "chapterSet"), "chapterSet"),
      requireNumber(tolerance, "tolerance"));
  }

  /**
   * Computes frame-level metadata for every chapter using the specified
   * frame-rate option.
   *
   * @param chapterSet - The chapter set to process.
   * @param options - Optional overrides:
   *   {@code optionCode} selects the frame-rate preset (defaults to
   *   {@code "Auto"}); {@code round} controls whether the frame count is
   *   rounded to the nearest integer (default {@code true}); {@code tolerance}
   *   sets the detection tolerance when using auto-detection (default 0.15).
   * @returns The chapter set augmented with frame-accuracy metadata.
   * @throws {TypeError} When {@code options.optionCode} is not a string.
   */
  async updateFrames(
    chapterSet: ChapterSet,
    options: { optionCode?: string; round?: boolean; tolerance?: number } = {},
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

  /**
   * Converts all chapter start times from one frame rate to another,
   * recalculating the `framesPerSecond` metadata on the resulting set.
   *
   * @param chapterSet - The chapter set to convert.
   * @param sourceFps - Original frame rate of the chapter set.
   * @param targetFps - Desired output frame rate.
   * @returns The converted chapter set and diagnostics.
   */
  async changeFrameRate(chapterSet: ChapterSet, sourceFps: number, targetFps: number): Promise<ChapterTransformResult> {
    return this.#invokeJson<ChapterTransformResult>(
      "ChangeFrameRate",
      encodeJson(requireObject(chapterSet, "chapterSet"), "chapterSet"),
      requireNumber(sourceFps, "sourceFps"),
      requireNumber(targetFps, "targetFps"));
  }

  /**
   * Evaluates a time-arithmetic expression against every chapter start time.
   * Expressions support variables ({@code t}, {@code n}), operators, and
   * functions from the expression engine.
   *
   * @param chapterSet - The chapter set to modify.
   * @param expression - Expression script text (e.g. {@code "t + 1"} adds one
   *   second to every chapter).
   * @param enabled - When {@code false} the expression is not applied but still
   *   validated (default {@code true}).
   * @returns The updated chapter set with any diagnostics.
   * @see {@link analyzeExpression} to preview token spans and completions
   *   before applying.
   * @see {@link expressionSymbols} for available variables and functions.
   */
  async applyExpression(chapterSet: ChapterSet, expression: string, enabled = true): Promise<ChapterEditResult> {
    return this.#invokeJson<ChapterEditResult>(
      "ApplyExpression",
      encodeJson(requireObject(chapterSet, "chapterSet"), "chapterSet"),
      requireBoolean(enabled, "enabled"),
      requireString(expression, "expression"));
  }

  /**
   * Projects how a chapter set would appear after export — resolving name
   * templates, order shifts, and encoding — without producing the final
   * output text.
   *
   * @param chapterSet - The chapter set to project.
   * @param options - Export-shaped options (primarily {@code format} and name
   *   generation flags).
   * @returns The original set plus the projected chapter list as it would be
   *   written on export.
   * @throws {TypeError} When {@code options.format} is missing or empty.
   */
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

  /**
   * Lexes and analyses an expression string, returning token spans, completion
   * suggestions at the caret position, and any diagnostics.
   *
   * @param expression - Expression script text to analyse.
   * @param options - Optional context for the analysis.
   * @param options.caretIndex - Zero-based caret offset for completion
   *   suggestions.  Defaults to {@code expression.length}.
   * @param options.timeSeconds - Current chapter time for variable resolution
   *   (default 0).
   * @param options.framesPerSecond - Frame rate for frame-related functions
   *   (default 24).
   * @returns Structured analysis with token spans, completions, and
   *   diagnostics.
   */
  async analyzeExpression(
    expression: string,
    options: { caretIndex?: number; timeSeconds?: number; framesPerSecond?: number } = {},
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

  /**
   * Returns the variables, functions, and operators available in the
   * expression engine.
   *
   * @returns Symbols that can be referenced in expression scripts.
   */
  async expressionSymbols(): Promise<ExpressionSymbol[]> {
    return this.#invokeJson<ExpressionSymbol[]>("GetExpressionSymbols");
  }

  /**
   * Returns the built-in expression presets shipped with the Core.
   *
   * @returns Preset definitions, each with an identifier, display name, and
   *   script text.
   */
  async expressionPresets(): Promise<ExpressionPreset[]> {
    return this.#invokeJson<ExpressionPreset[]>("GetExpressionPresets");
  }

  /**
   * Parses a time-text string into fractional seconds, returning diagnostics
   * when the input is ambiguous or invalid.
   *
   * @param text - Time text such as {@code "01:23:45.678"}.
   * @returns The parsed duration together with any warnings or errors.
   */
  async parseTime(text: string): Promise<TimeParseResult> {
    return this.#invokeJson<TimeParseResult>("ParseTime", requireString(text, "text"));
  }

  /**
   * Lenient variant of {@link parseTime} that returns {@code 0} for any input
   * that cannot be parsed.
   *
   * @param text - Time text to attempt parsing.
   * @returns Parsed seconds, or 0 on failure.
   */
  async parseTimeOrZero(text: string): Promise<number> {
    return this.#invoke<number>("ParseTimeOrZero", requireString(text, "text"));
  }

  /**
   * Formats a duration in seconds as chapter time-text.
   *
   * @param seconds - Duration in fractional seconds.
   * @returns Formatted time string (e.g. {@code "00:05:23.500"}).
   */
  async formatTime(seconds: number): Promise<string> {
    return this.#invoke<string>("FormatTime", requireNumber(seconds, "seconds"));
  }

  /**
   * Formats a duration in seconds as cue-sheet time-text (no sub-second
   * precision).
   *
   * @param seconds - Duration in fractional seconds.
   * @returns Time string in {@code HH:MM:SS} format.
   */
  async formatCueTime(seconds: number): Promise<string> {
    return this.#invoke<string>("FormatCueTime", requireNumber(seconds, "seconds"));
  }

  /**
   * Converts a chapter set to Celltimes format (frame-count lines for use with
   * video encoders).
   *
   * @param chapterSet - The chapter set to convert.
   * @param framesPerSecond - Frame rate for the frame-count calculation.
   * @returns Celltimes text content and diagnostics.
   */
  async toCelltimes(chapterSet: ChapterSet, framesPerSecond: number): Promise<ChapterConversionResult> {
    return this.#invokeJson<ChapterConversionResult>(
      "ToCelltimes",
      encodeJson(requireObject(chapterSet, "chapterSet"), "chapterSet"),
      requireNumber(framesPerSecond, "framesPerSecond"));
  }

  /**
   * Converts raw chapter-text content into QPFile format (keyframe index for
   * x264 / x265).
   *
   * @param chapterText - OGM-style chapter text.
   * @param framesPerSecond - Frame rate used for the keyframe calculation.
   * @param timecodeText - Optional initial timecode text.  When
   *   {@code null} or omitted, the first chapter starts at frame 0.
   * @returns QPFile text content and diagnostics.
   */
  async chapterTextToQpfile(
    chapterText: string,
    framesPerSecond: number,
    timecodeText: string | null = null,
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

  /**
   * Returns the XML language options available for XML-based export formats.
   *
   * @returns Language descriptors with BCP 47 / ISO 639-1 codes.
   */
  async xmlLanguages(): Promise<XmlLanguage[]> {
    return this.#invokeJson<XmlLanguage[]>("GetXmlLanguages");
  }

  /**
   * Returns the text-encoding options available for chapter output.
   *
   * @returns Encoding descriptors with internal identifiers and XML names.
   */
  async outputEncodings(): Promise<OutputEncoding[]> {
    return this.#invokeJson<OutputEncoding[]>("GetOutputEncodings");
  }
}
