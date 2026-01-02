import {themes as prismThemes} from 'prism-react-renderer';
import type {Config} from '@docusaurus/types';
import type * as Preset from '@docusaurus/preset-classic';

const config: Config = {
  title: 'ClipMate',
  tagline: 'The Ultimate Clipboard Manager for Windows',
  favicon: 'img/favicon.ico',

  // GitHub Pages deployment configuration
  url: 'https://tsabo.github.io',
  baseUrl: '/ClipMate/',

  // GitHub pages deployment config
  organizationName: 'tsabo',
  projectName: 'ClipMate',

  onBrokenLinks: 'throw',
  onBrokenMarkdownLinks: 'warn',

  i18n: {
    defaultLocale: 'en',
    locales: ['en'],
  },

  presets: [
    [
      'classic',
      {
        docs: {
          sidebarPath: './sidebars.ts',
          routeBasePath: '/',
          editUrl: 'https://github.com/tsabo/ClipMate/tree/main/docs/',
        },
        blog: false,
        theme: {
          customCss: './src/css/custom.css',
        },
      } satisfies Preset.Options,
    ],
  ],

  themeConfig: {
    image: 'img/clipmate-social-card.png',
    navbar: {
      title: 'ClipMate',
      logo: {
        alt: 'ClipMate Logo',
        src: 'img/logo.svg',
      },
      items: [
        {
          type: 'docSidebar',
          sidebarId: 'docsSidebar',
          position: 'left',
          label: 'Documentation',
        },
        {
          href: 'https://github.com/tsabo/ClipMate',
          label: 'GitHub',
          position: 'right',
        },
      ],
    },
    footer: {
      style: 'dark',
      links: [
        {
          title: 'Documentation',
          items: [
            {
              label: 'Introduction',
              to: '/',
            },
            {
              label: 'Tutorial',
              to: '/tutorial',
            },
          ],
        },
        {
          title: 'Community',
          items: [
            {
              label: 'GitHub Discussions',
              href: 'https://github.com/tsabo/ClipMate/discussions',
            },
            {
              label: 'Issues',
              href: 'https://github.com/tsabo/ClipMate/issues',
            },
          ],
        },
        {
          title: 'More',
          items: [
            {
              label: 'GitHub',
              href: 'https://github.com/tsabo/ClipMate',
            },
            {
              label: 'Releases',
              href: 'https://github.com/tsabo/ClipMate/releases',
            },
          ],
        },
      ],
      copyright: `Inspired by ClipMate® by Thornsoft Development. This project © ${new Date().getFullYear()} Jeremy Brown and ClipMate Contributors.`,
    },
    prism: {
      theme: prismThemes.github,
      darkTheme: prismThemes.dracula,
    },
  } satisfies Preset.ThemeConfig,
};

export default config;
