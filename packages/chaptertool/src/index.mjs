import { dotnet } from "./runtime/_framework/dotnet.js";

let apiPromise;

async function getApi() {
  if (!apiPromise) {
    apiPromise = (async () => {
      const runtime = await dotnet
        .withMainAssembly("ChapterTool.Node")
        .create();
      const assembly = await runtime.getAssemblyExports("ChapterTool.Node");
      return assembly.ChapterTool.Node.NodeApi;
    })();
  }

  return apiPromise;
}

function toBytes(content) {
  if (typeof content === "string") {
    return Buffer.from(content, "utf8");
  }

  if (Buffer.isBuffer(content)) {
    return content;
  }

  if (content instanceof Uint8Array) {
    return Buffer.from(content);
  }

  throw new TypeError("Chapter content must be a string, Buffer, or Uint8Array.");
}

function requireFileName(fileName) {
  if (fileName === undefined) {
    return "input.txt";
  }

  if (typeof fileName !== "string" || fileName.trim().length === 0) {
    throw new TypeError("fileName must be a non-empty string.");
  }

  return fileName.trim();
}

function requireObject(value, name) {
  if (!value || typeof value !== "object") {
    throw new TypeError(`${name} must be an object.`);
  }

  return value;
}

function requireNumber(value, name) {
  if (typeof value !== "number" || !Number.isFinite(value)) {
    throw new TypeError(`${name} must be a finite number.`);
  }

  return value;
}

/**
 * Creates a Node.js ChapterTool client backed by the .NET WebAssembly Core.
 * @returns {Promise<object>}
 */
export async function createChapterTool() {
  const api = await getApi();

  return {
    async import(content, options = {}) {
      const fileName = requireFileName(options.fileName);
      const bytes = toBytes(content);
      return JSON.parse(api.Import(fileName, bytes.toString("base64")));
    },

    async export(chapterSet, options) {
      requireObject(chapterSet, "chapterSet");

      if (!options || typeof options !== "object" || typeof options.format !== "string") {
        throw new TypeError("options.format must be an export format code, such as 'xml'.");
      }

      return JSON.parse(api.Export(JSON.stringify(chapterSet), JSON.stringify(options)));
    },

    async formats() {
      return JSON.parse(api.GetFormats());
    },

    async importFormats() {
      return JSON.parse(api.GetImportFormats());
    },

    async isBinaryExtension(extension) {
      if (typeof extension !== "string") {
        throw new TypeError("extension must be a string.");
      }
      return api.IsBinaryExtension(extension);
    },

    async editTime(chapterSet, index, text) {
      return JSON.parse(api.Edit(JSON.stringify(requireObject(chapterSet, "chapterSet")), "editTime", JSON.stringify({ index, text })));
    },

    async editFrame(chapterSet, index, text, framesPerSecond) {
      return JSON.parse(api.Edit(JSON.stringify(requireObject(chapterSet, "chapterSet")), "editFrame", JSON.stringify({ index, text, framesPerSecond: requireNumber(framesPerSecond, "framesPerSecond") })));
    },

    async rename(chapterSet, index, name) {
      return JSON.parse(api.Edit(JSON.stringify(requireObject(chapterSet, "chapterSet")), "rename", JSON.stringify({ index, text: name })));
    },

    async delete(chapterSet, indexes) {
      if (!Array.isArray(indexes)) {
        throw new TypeError("indexes must be an array of zero-based chapter indexes.");
      }
      return JSON.parse(api.Edit(JSON.stringify(requireObject(chapterSet, "chapterSet")), "delete", JSON.stringify({ indexes })));
    },

    async insertBefore(chapterSet, index) {
      return JSON.parse(api.Edit(JSON.stringify(requireObject(chapterSet, "chapterSet")), "insertBefore", JSON.stringify({ index })));
    },

    async applyOrderShift(chapterSet, shift) {
      return JSON.parse(api.Edit(JSON.stringify(requireObject(chapterSet, "chapterSet")), "applyOrderShift", JSON.stringify({ shift })));
    },

    async applyTemplate(chapterSet, templateText) {
      return JSON.parse(api.Edit(JSON.stringify(requireObject(chapterSet, "chapterSet")), "applyTemplate", JSON.stringify({ text: templateText })));
    },

    async shiftFramesForward(chapterSet, frames, framesPerSecond) {
      return JSON.parse(api.Edit(JSON.stringify(requireObject(chapterSet, "chapterSet")), "shiftFramesForward", JSON.stringify({ frames, framesPerSecond: requireNumber(framesPerSecond, "framesPerSecond") })));
    },

    async createZones(chapterSet, indexes, framesPerSecond) {
      if (!Array.isArray(indexes)) {
        throw new TypeError("indexes must be an array of zero-based chapter indexes.");
      }
      return JSON.parse(api.CreateZones(JSON.stringify(requireObject(chapterSet, "chapterSet")), JSON.stringify(indexes), requireNumber(framesPerSecond, "framesPerSecond")));
    },

    async combine(source) {
      return JSON.parse(api.Combine(JSON.stringify(requireObject(source, "source"))));
    },

    async append(existing, appended) {
      return JSON.parse(api.Append(
        JSON.stringify(requireObject(existing, "existing")),
        JSON.stringify(requireObject(appended, "appended"))));
    },

    async frameRates() {
      return JSON.parse(api.GetFrameRates());
    },

    async findFrameRate(framesPerSecond) {
      return JSON.parse(api.FindFrameRate(requireNumber(framesPerSecond, "framesPerSecond")));
    },

    async detectFrameRate(chapterSet, tolerance = 0.15) {
      return JSON.parse(api.DetectFrameRate(JSON.stringify(requireObject(chapterSet, "chapterSet")), requireNumber(tolerance, "tolerance")));
    },

    async updateFrames(chapterSet, options = {}) {
      return JSON.parse(api.UpdateFrames(
        JSON.stringify(requireObject(chapterSet, "chapterSet")),
        options.optionCode ?? "Auto",
        options.round ?? true,
        requireNumber(options.tolerance ?? 0.15, "tolerance")));
    },

    async changeFrameRate(chapterSet, sourceFps, targetFps) {
      return JSON.parse(api.ChangeFrameRate(
        JSON.stringify(requireObject(chapterSet, "chapterSet")),
        requireNumber(sourceFps, "sourceFps"),
        requireNumber(targetFps, "targetFps")));
    },

    async applyExpression(chapterSet, expression, enabled = true) {
      return JSON.parse(api.ApplyExpression(
        JSON.stringify(requireObject(chapterSet, "chapterSet")),
        enabled,
        expression));
    },

    async project(chapterSet, options) {
      requireObject(options, "options");
      return JSON.parse(api.Project(
        JSON.stringify(requireObject(chapterSet, "chapterSet")),
        JSON.stringify(options)));
    },

    async analyzeExpression(expression, options = {}) {
      return JSON.parse(api.AnalyzeExpression(
        expression,
        options.caretIndex ?? expression.length,
        requireNumber(options.timeSeconds ?? 0, "timeSeconds"),
        requireNumber(options.framesPerSecond ?? 24, "framesPerSecond")));
    },

    async expressionSymbols() {
      return JSON.parse(api.GetExpressionSymbols());
    },

    async expressionPresets() {
      return JSON.parse(api.GetExpressionPresets());
    },

    async parseTime(text) {
      return JSON.parse(api.ParseTime(text));
    },

    async parseTimeOrZero(text) {
      return api.ParseTimeOrZero(text);
    },

    async formatTime(seconds) {
      return api.FormatTime(requireNumber(seconds, "seconds"));
    },

    async formatCueTime(seconds) {
      return api.FormatCueTime(requireNumber(seconds, "seconds"));
    },

    async toCelltimes(chapterSet, framesPerSecond) {
      return JSON.parse(api.ToCelltimes(
        JSON.stringify(requireObject(chapterSet, "chapterSet")),
        requireNumber(framesPerSecond, "framesPerSecond")));
    },

    async chapterTextToQpfile(chapterText, framesPerSecond, timecodeText = null) {
      return JSON.parse(api.ChapterTextToQpfile(
        chapterText,
        requireNumber(framesPerSecond, "framesPerSecond"),
        timecodeText));
    },

    async xmlLanguages() {
      return JSON.parse(api.GetXmlLanguages());
    },

    async outputEncodings() {
      return JSON.parse(api.GetOutputEncodings());
    }
  };
}

export async function importChapters(content, options = {}) {
  const tool = await createChapterTool();
  return tool.import(content, options);
}

export async function exportChapters(chapterSet, options) {
  const tool = await createChapterTool();
  return tool.export(chapterSet, options);
}
