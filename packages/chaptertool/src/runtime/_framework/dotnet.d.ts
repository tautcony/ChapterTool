export interface DotnetRuntime {
  withMainAssembly(name: string): {
    create(): Promise<DotnetRuntimeInstance>;
  };
}

export interface DotnetRuntimeInstance {
  getAssemblyExports<T>(assemblyName: string): Promise<T>;
}

export declare const dotnet: DotnetRuntime;
