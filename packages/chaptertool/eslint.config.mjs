import eslint from "@eslint/js";
import tseslint from "typescript-eslint";

export default tseslint.config(
  eslint.configs.recommended,
  ...tseslint.configs.recommended,
  {
    rules: {
      // Style — matches the existing codebase conventions.
      "indent": ["error", 2, { SwitchCase: 1 }],
      "quotes": ["error", "double", { avoidEscape: true }],
      "semi": ["error", "always"],
      "comma-dangle": ["error", "always-multiline"],
      "no-var": "error",
      "prefer-const": "error",
      "prefer-arrow-callback": "error",
      "no-trailing-spaces": "error",
      "eol-last": ["error", "always"],
      "no-multiple-empty-lines": ["error", { max: 1, maxEOF: 0 }],

      // TypeScript adjustments.
      "@typescript-eslint/no-unused-vars": ["error", { argsIgnorePattern: "^_" }],
      "@typescript-eslint/explicit-function-return-type": "off",
      "@typescript-eslint/consistent-type-imports": ["error", { prefer: "type-imports", fixStyle: "separate-type-imports" }],

      // Relaxations for a library package.
      "@typescript-eslint/no-explicit-any": "off",
    },
  },
  {
    // Scripts and tests use .mjs and rely on Node.js globals.
    files: ["scripts/**/*.mjs", "test/**/*.mjs", "vitest.config.mjs", "eslint.config.mjs"],
    languageOptions: {
      globals: {
        console: "readonly",
        process: "readonly",
        URL: "readonly",
        Buffer: "readonly",
        TextEncoder: "readonly",
      },
    },
    rules: {
      "@typescript-eslint/no-require-imports": "off",
    },
  },
  {
    ignores: [
      "dist/",
      "src/runtime/",
      "node_modules/",
      "*.d.ts",
    ],
  },
);
