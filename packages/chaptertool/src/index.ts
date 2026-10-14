import { dotnet } from "./runtime/_framework/dotnet.js";
import { createRetryableLoader } from "./api-loader.js";
import { applyMaxInputBytes } from "./utils/input.js";
import { ExactChapterTool } from "./exact.js";

export type {
  ChapterConversionResult,
  ChapterDiagnostic,
  ChapterExportResult,
  ChapterFormat,
  ChapterImportFormat,
  ChapterInput,
  ChapterOutputEncoding,
  ChapterXmlLanguage,
  ChapterZonesResult,
} from "./types.js";
export type * from "./exact-types.js";
export { MAX_INPUT_BYTES } from "./utils/input.js";

interface NodeApi {
  [operation: string]: (...arguments_: unknown[]) => unknown;
  GetMaxInputBytes(): number;
}

interface ChapterToolAssembly {
  ChapterTool: { Node: { NodeApi: NodeApi } };
}

/** Exact tick and frame-rate API for ChapterTool's Node.js package. */
export class ChapterTool extends ExactChapterTool {
  private static readonly loadApi = createRetryableLoader(async (): Promise<NodeApi> => {
    const runtime = await dotnet.withMainAssembly("ChapterTool.Node").create();
    const assembly = await runtime.getAssemblyExports<ChapterToolAssembly>("ChapterTool.Node");
    const api = assembly.ChapterTool.Node.NodeApi;
    applyMaxInputBytes(Number(api.GetMaxInputBytes()));
    return api;
  });

  constructor() {
    super(
      async <T>(operation: string, ...arguments_: unknown[]): Promise<T> => {
        const api = await ChapterTool.loadApi();
        const result = api[operation](...arguments_);
        return (typeof result === "string" ? JSON.parse(result) : result) as T;
      },
      async () => { await ChapterTool.loadApi(); },
    );
  }
}
