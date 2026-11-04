import type {SidebarsConfig} from '@docusaurus/plugin-content-docs';

const sidebars: SidebarsConfig = {
  docsSidebar: [
    'intro',
    {
      type: 'category',
      label: 'Get started',
      collapsed: false,
      items: [
        'get-started/install',
        'get-started/first-run',
        'get-started/first-clips',
        'get-started/the-two-windows',
        'get-started/uninstall',
      ],
    },
    {
      type: 'category',
      label: 'Guides',
      items: [
        {
          type: 'category',
          label: 'Tutorial',
          items: [
            'tutorial/index',
            'tutorial/lesson-one-basic-operation',
            'tutorial/lesson-two-powerpaste',
            'tutorial/lesson-three-append',
            'tutorial/lesson-four-quickpaste',
            'tutorial/lesson-five-editing',
            'tutorial/lesson-six-quickpick',
            'tutorial/lesson-seven-shortcuts',
          ],
        },
      ],
    },
    {
      type: 'category',
      label: 'Use ClipMate across your Devices',
      link: {type: 'doc', id: 'devices/index'},
      items: [
        'devices/pair',
        'devices/share-and-search',
        'devices/copy-to-local',
      ],
    },
    {
      type: 'category',
      label: 'Coming from ClipMate 7',
      items: [
        'coming-from-clipmate-7/import',
        'coming-from-clipmate-7/whats-new',
        'coming-from-clipmate-7/whats-different',
      ],
    },
    {
      type: 'category',
      label: 'Reference',
      items: [
        {
          type: 'category',
          label: 'The User Interface',
          items: [
            'user-interface/index',
            'user-interface/clipmate-classic',
            'user-interface/clipmate-explorer',
            {
              type: 'category',
              label: 'Main Toolbar',
              items: [
                'user-interface/main-toolbar/index',
                'user-interface/main-toolbar/powerpaste',
                'user-interface/main-toolbar/delete-clips',
                'user-interface/main-toolbar/print',
                'user-interface/main-toolbar/append',
                'user-interface/main-toolbar/move-clips-to-collection',
                'user-interface/main-toolbar/copy-clips-to-collection',
                'user-interface/main-toolbar/select-collection',
                'user-interface/main-toolbar/search',
                'user-interface/main-toolbar/switch-classic-explorer',
                'user-interface/main-toolbar/view-clip',
                'user-interface/main-toolbar/view-mode',
                'user-interface/main-toolbar/outbound-clip-filtering',
                'user-interface/main-toolbar/templates',
                'user-interface/main-toolbar/event-log',
              ],
            },
            {
              type: 'category',
              label: 'ClipList',
              items: [
                'user-interface/cliplist/index',
                'user-interface/cliplist/sorting-clips',
                'user-interface/cliplist/source-url',
                'user-interface/cliplist/detail-view',
                'user-interface/cliplist/item-type-icons',
                'user-interface/cliplist/thumbnail-view',
              ],
            },
            {
              type: 'category',
              label: 'Preview/Edit Window',
              items: [
                'user-interface/preview-edit/index',
                'user-interface/preview-edit/text-tab',
                'user-interface/preview-edit/rich-text-tab',
                'user-interface/preview-edit/bitmap-tab',
                'user-interface/preview-edit/picture-tab',
                'user-interface/preview-edit/html-tab',
                'user-interface/preview-edit/binary-tab',
              ],
            },
            'user-interface/visibility',
          ],
        },
        {
          type: 'category',
          label: 'Options and Configuration',
          items: [
            'options/index',
            'options/general',
            'options/visual',
            'options/editor',
            'options/capturing',
            'options/application-profile',
            'options/pasting',
            'options/quickpaste',
            'options/hotkeys',
            'options/sounds',
            'options/databases',
            'options/advanced-options',
            'options/devices',
          ],
        },
        {
          type: 'category',
          label: 'Advanced',
          items: [
            'advanced/index',
            'advanced/templates',
            'advanced/macro-pasting',
            'advanced/data-management',
          ],
        },
        'glossary',
      ],
    },
    {
      type: 'category',
      label: 'Troubleshooting',
      items: [
        'troubleshooting/network',
      ],
    },
  ],
};

export default sidebars;
