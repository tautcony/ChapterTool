import { defineConfig } from 'vitepress';

export default defineConfig({
  lang: 'en-US',
  title: 'ChapterTool Documentation',
  description: 'Generated API reference for ChapterTool.Core.',
  base: '/ChapterTool/docs/',
  outDir: '../artifacts/core-api-docs/site',
  themeConfig: {
    nav: [
      { text: 'API Reference', link: '/api/' },
      { text: 'Browser App', link: 'https://tautcony.github.io/ChapterTool/' },
    ],
    sidebar: {
      '/api/': [
        {
          text: 'ChapterTool.Core',
          items: [
            { text: 'Overview', link: '/api/' },
            { text: 'Boundaries', link: '/api/ChapterTool.Core.Boundaries' },
            { text: 'Diagnostics', link: '/api/ChapterTool.Core.Diagnostics' },
            { text: 'Editing', link: '/api/ChapterTool.Core.Editing' },
            { text: 'Exporting', link: '/api/ChapterTool.Core.Exporting' },
            { text: 'Importing', link: '/api/ChapterTool.Core.Importing' },
            { text: 'CUE Import', link: '/api/ChapterTool.Core.Importing.Cue' },
            { text: 'Disc Import', link: '/api/ChapterTool.Core.Importing.Disc' },
            { text: 'Media Import', link: '/api/ChapterTool.Core.Importing.Media' },
            { text: 'Text Import', link: '/api/ChapterTool.Core.Importing.Text' },
            { text: 'Localization', link: '/api/ChapterTool.Core.Localization' },
            { text: 'Models', link: '/api/ChapterTool.Core.Models' },
            { text: 'Session', link: '/api/ChapterTool.Core.Session' },
            { text: 'Transform', link: '/api/ChapterTool.Core.Transform' },
            { text: 'Expressions', link: '/api/ChapterTool.Core.Transform.Expressions' },
            { text: 'Lua', link: '/api/ChapterTool.Core.Transform.Expressions.Lua' },
          ],
        },
      ],
    },
    socialLinks: [
      { icon: 'github', link: 'https://github.com/tautcony/ChapterTool' },
    ],
  },
});
