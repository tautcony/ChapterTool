## 1. Resource Storage

- [x] 1.1 Create culture-specific `.resx` files for Simplified Chinese, English, and Japanese using the existing localization keys and values.
- [x] 1.2 Ensure the Avalonia project builds the `.resx` files as embedded .NET resources without requiring hand-maintained designer accessors.

## 2. Localization Loader

- [x] 2.1 Replace hard-coded translation dictionaries in `AppLocalizationResources` with a `ResourceManager`-backed loader facade.
- [x] 2.2 Keep current culture normalization, Simplified Chinese fallback, formatting, and Avalonia application resource update behavior stable.

## 3. Tests and Validation

- [x] 3.1 Update localization tests to validate the compiled resource dictionaries for key parity, placeholder parity, mojibake protection, fallback, and formatting.
- [x] 3.2 Run focused Avalonia tests and build validation for the Avalonia app.
