using ChapterTool.Wasm;
using ChapterTool.Wasm.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped<WasmChapterService>();
builder.Services.AddScoped<WasmWorkspace>();
builder.Services.AddScoped<WasmLocalizer>();

await builder.Build().RunAsync();
