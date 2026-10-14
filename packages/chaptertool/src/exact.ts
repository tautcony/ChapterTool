import { requireFileName, toBytes, MAX_INPUT_BYTES } from "./utils/input.js";
import { encodeJson } from "./utils/json.js";
import { requireString } from "./utils/validation.js";
import type { ChapterConversionResult, ChapterExportResult, ChapterInput, ChapterFormat, ChapterImportFormat,
  ChapterOutputEncoding, ChapterXmlLanguage, ChapterZonesResult } from "./types.js";
import type {
  ExactChapterSet, ExactContentResult, ExactDetectionResult, ExactFrameInfoResult, ExactFrameRate,
  ExactFrameRateOption, ExactImportGroup, ExactImportResult, ExactOutputOptions,
} from "./exact-types.js";

type ExactInvoker = <T>(operation: string, ...arguments_: unknown[]) => Promise<T>;
const json = (value: unknown): string => encodeJson(value, "exact input");
const rateJson = (rate?: ExactFrameRate): string | null => rate === undefined ? null : json(rate);

/** Lossless content API. Rates and timestamps are validated by the canonical Core boundary. */
export class ExactChapterTool {
  constructor(private readonly invoke: ExactInvoker, private readonly load: () => Promise<void>) {}

  async maxInputBytes(): Promise<number> {
    await this.load();
    return MAX_INPUT_BYTES;
  }

  formats(): Promise<ChapterFormat[]> {
    return this.invoke("GetFormats");
  }

  importFormats(): Promise<ChapterImportFormat[]> {
    return this.invoke("GetImportFormats");
  }

  isBinaryExtension(extension: string): Promise<boolean> {
    return this.invoke("IsBinaryExtension", requireString(extension, "extension"));
  }

  xmlLanguages(): Promise<ChapterXmlLanguage[]> {
    return this.invoke("GetXmlLanguages");
  }

  outputEncodings(): Promise<ChapterOutputEncoding[]> {
    return this.invoke("GetOutputEncodings");
  }

  async import(content: ChapterInput, options: { fileName?: string } = {}): Promise<ExactImportResult> {
    await this.load();
    return this.invoke("ExactImport", requireFileName(options.fileName), toBytes(content).toString("base64"));
  }

  private edit(set: ExactChapterSet, operation: string, options: object): Promise<ExactContentResult> {
    return this.invoke("ExactEdit", json(set), operation, json(options));
  }

  editTime(set: ExactChapterSet, index: number, text: string): Promise<ExactContentResult> {
    return this.edit(set, "editTime", { index, text });
  }

  editStartTicks(set: ExactChapterSet, index: number, ticks: string): Promise<ExactContentResult> {
    return this.edit(set, "editStartTicks", { index, ticks });
  }

  editFrame(set: ExactChapterSet, index: number, text: string, computationRate?: ExactFrameRate): Promise<ExactContentResult> {
    return this.edit(set, "editFrame", { index, text, computationRate });
  }

  rename(set: ExactChapterSet, index: number, text: string): Promise<ExactContentResult> {
    return this.edit(set, "rename", { index, text });
  }

  delete(set: ExactChapterSet, indexes: number[]): Promise<ExactContentResult> {
    return this.edit(set, "delete", { indexes });
  }

  insertBefore(set: ExactChapterSet, index: number): Promise<ExactContentResult> {
    return this.edit(set, "insertBefore", { index });
  }

  applyOrderShift(set: ExactChapterSet, shift: number): Promise<ExactContentResult> {
    return this.edit(set, "applyOrderShift", { shift });
  }

  applyTemplate(set: ExactChapterSet, text: string): Promise<ExactContentResult> {
    return this.edit(set, "applyTemplate", { text });
  }

  shiftFramesForward(set: ExactChapterSet, frames: number, computationRate?: ExactFrameRate): Promise<ExactContentResult> {
    return this.edit(set, "shiftFramesForward", { frames, computationRate });
  }

  createZones(set: ExactChapterSet, indexes: number[], computationRate?: ExactFrameRate): Promise<ChapterZonesResult> {
    return this.invoke("ExactCreateZones", json(set), json(indexes), rateJson(computationRate));
  }

  combine(group: ExactImportGroup): Promise<ExactContentResult> {
    return this.invoke("ExactCombine", json(group));
  }

  append(existing: ExactImportGroup, appended: ExactImportGroup): Promise<ExactContentResult> {
    return this.invoke("ExactAppend", json(existing), json(appended));
  }

  frameRates(): Promise<ExactFrameRateOption[]> {
    return this.invoke("ExactGetFrameRates");
  }

  detectFrameRate(set: ExactChapterSet, tolerance = 0.01): Promise<ExactDetectionResult> {
    return this.invoke("ExactDetectFrameRate", json(set), tolerance);
  }

  updateFrames(set: ExactChapterSet, computationRate?: ExactFrameRate, decimalPlaces = 0, tolerance = 0.01): Promise<ExactFrameInfoResult> {
    return this.invoke("ExactUpdateFrames", json(set), rateJson(computationRate), decimalPlaces, tolerance);
  }

  changeFrameRate(set: ExactChapterSet, sourceRate: ExactFrameRate, targetRate: ExactFrameRate): Promise<ExactContentResult> {
    return this.invoke("ExactChangeFrameRate", json(set), json(sourceRate), json(targetRate));
  }

  applyExpression(set: ExactChapterSet, enabled: boolean, expression: string, computationRate?: ExactFrameRate): Promise<ExactContentResult> {
    return this.invoke("ExactApplyExpression", json(set), enabled, expression, rateJson(computationRate));
  }

  serialize(set: ExactChapterSet, options: ExactOutputOptions): Promise<ChapterExportResult> {
    return this.invoke("ExactSerialize", json(set), json(options));
  }

  toCelltimes(set: ExactChapterSet, computationRate?: ExactFrameRate): Promise<ChapterConversionResult> {
    return this.invoke("ExactToCelltimes", json(set), rateJson(computationRate));
  }

  chapterTextToQpfile(text: string, computationRate?: ExactFrameRate, timecodeText: string | null = null): Promise<ChapterConversionResult> {
    return this.invoke("ExactChapterTextToQpfile", text, rateJson(computationRate), timecodeText);
  }
}
