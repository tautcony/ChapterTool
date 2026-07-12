using ChapterTool.Core.WasmDemo;
using ChapterTool.Core.WasmDemo.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped<ChapterDemoService>();
builder.Services.AddScoped<DemoWorkspace>();

await builder.Build().RunAsync();
