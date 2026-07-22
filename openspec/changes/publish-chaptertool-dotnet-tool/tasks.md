## 1. Tool Package

- [x] 1.1 Configure `ChapterTool.Cli` as the `ChapterTool` .NET Tool with the `chaptertool` command and unified version metadata.
- [x] 1.2 Add a package README that defines installation, update, removal, runtime, and external-tool requirements.
- [x] 1.3 Pack the project and verify the package identity and dependency layout.

## 2. Release Automation

- [x] 2.1 Rename `publish-app` to `pack-dotnet` and publish Avalonia plus the CLI package in each runtime matrix job.
- [x] 2.2 Remove temporary CLI installation from CI and the tag-triggered NuGet workflow.
- [x] 2.3 Upload both outputs in one named CI artifact for each runtime.

## 3. Documentation

- [x] 3.1 Update the main README with the NuGet Tool workflow and standalone CLI examples.
- [x] 3.2 Update the distribution and CLI code maps with package ownership and verification commands.

## 4. Verification

- [x] 4.1 Run the focused Avalonia CLI tests.
- [x] 4.2 Run the full solution tests in sequence.
- [x] 4.3 Validate the OpenSpec change in strict mode.
