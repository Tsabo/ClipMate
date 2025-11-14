# Feature Plan: ClipBar Quick Access Popup

**Feature ID**: 002-clipbar-quick-access  
**Created**: 2025-11-14  
**Status**: Planning  
**Priority**: P1 (Core productivity feature)  
**Dependencies**: 001-clipboard-manager (clipboard capture must work)

## Problem Statement

Users need instant access to their clipboard history from any application without switching windows. The current "PowerPaste" implementation is misnamed and doesn't match ClipMate 7.5's actual functionality. We need to:

1. **Rename & Clarify**: Current "PowerPaste" is actually "ClipBar-style Quick Access"
2. **Complete Implementation**: Add actual paste functionality (currently just shows clips)
3. **Improve UI Density**: Current UI has too much padding/spacing - looks like a modern web app instead of a Windows utility
4. **Match ClipMate 7.5 Style**: Compact, dense list with minimal chrome

## Goals

### Primary Goals
1. **Quick Access Popup** - Ctrl+Shift+V shows compact popup with recent clips
2. **Instant Paste** - Select clip → immediately pastes to active application
3. **Windows Utility Aesthetics** - Dense, compact UI matching ClipMate 7.5 style
4. **Keyboard-First UX** - All operations accessible via keyboard
5. **Performance** - Popup appears instantly (<100ms), search filters instantly

### Non-Goals (Future Features)
- Taskbar integration (actual ClipBar docking) - Phase 2
- PowerPaste transformations (paste-with-formatting) - Separate feature
- Advanced editing in popup - Keep it simple

## Success Criteria

### Functional Requirements
- [ ] Hotkey (Ctrl+Shift+V) opens popup instantly from any application
- [ ] Popup shows 20 most recent clips in compact list format
- [ ] Search box filters results as user types (instant filtering)
- [ ] Arrow keys navigate list
- [ ] Enter key pastes selected clip to active window and closes popup
- [ ] Escape key closes popup without pasting
- [ ] Popup positioned near cursor/caret (not centered on screen)
- [ ] Mouse click on clip pastes and closes popup

### UI Requirements - Windows Utility Style
- [ ] **Compact List**: 18-20px row height (not 24px+)
- [ ] **Minimal Padding**: 2-4px padding (not 8-12px)
- [ ] **Dense Font**: 9pt Segoe UI (not 11-12pt)
- [ ] **Subtle Colors**: System colors, no gradients or shadows
- [ ] **Thin Borders**: 1px borders (not 2-3px)
- [ ] **Small Icons**: 16x16px icons (not 24x24px)
- [ ] **No Rounded Corners**: Sharp, utility-style borders
- [ ] **Minimal Chrome**: Thin title bar or no title bar

### Performance Requirements
- [ ] Popup appears in <100ms after hotkey press
- [ ] Search filtering <50ms response time
- [ ] Paste operation completes in <200ms
- [ ] Memory footprint <5MB for popup window

## User Stories

### US1: Quick Paste from Any Application
**As a** user working in any application  
**I want to** press Ctrl+Shift+V and see my recent clipboard items  
**So that** I can quickly paste a previous clip without switching windows

**Acceptance Criteria:**
- Hotkey works globally in any application
- Popup appears near cursor position
- Shows 20 most recent clips
- Clips show preview (first line, truncated)
- Selection via keyboard or mouse
- Enter/Click pastes and closes popup

### US2: Search and Filter Clips
**As a** power user with many clipboard items  
**I want to** type search terms in the popup  
**So that** I can quickly find specific clips without scrolling

**Acceptance Criteria:**
- Search box has focus when popup opens
- Typing filters list instantly
- Search matches content (case-insensitive)
- Search highlights matching text
- Clear search button or Ctrl+A to select all

### US3: Compact Windows Utility Appearance
**As a** Windows power user  
**I want** the popup to look like a native Windows utility (compact, dense)  
**Not** like a modern web application with excessive padding

**Acceptance Criteria:**
- Row height 18-20px (matches Windows Explorer detail view)
- Font size 9pt (matches Windows system dialogs)
- Minimal padding 2-4px
- System theme colors
- No gradients, shadows, or rounded corners
- Thin 1px borders

## Technical Approach

### Architecture

```
┌─────────────────────────────────────┐
│   Any Application (Active Window)   │
└──────────────┬──────────────────────┘
               │
               │ Ctrl+Shift+V
               ↓
┌─────────────────────────────────────┐
│   QuickAccessCoordinator             │
│   - Registers hotkey                 │
│   - Shows QuickAccessWindow          │
└──────────────┬──────────────────────┘
               │
               ↓
┌─────────────────────────────────────┐
│   QuickAccessWindow (Popup)         │
│   - Compact list of clips           │
│   - Search box                       │
│   - Keyboard navigation              │
└──────────────┬──────────────────────┘
               │
               ↓
┌─────────────────────────────────────┐
│   PasteService                       │
│   - Gets active window handle        │
│   - Sends clipboard content          │
│   - Uses SendKeys or SendInput       │
└─────────────────────────────────────┘
```

### Components to Rename

**Current → New:**
- `PowerPasteWindow` → `QuickAccessWindow`
- `PowerPasteViewModel` → `QuickAccessViewModel`
- `PowerPasteCoordinator` → `QuickAccessCoordinator`
- `PowerPasteWindowTests` → `QuickAccessWindowTests`

**Reserve "PowerPaste" for future paste-transformation feature**

### UI Redesign - Windows Utility Style

**Before (Current - Too Modern):**
```
┌─────────────────────────────────────┐
│  ClipMate - Recent Clips            │ ← 30px title bar
├─────────────────────────────────────┤
│  [Search...              ]          │ ← 12px padding, 32px search box
├─────────────────────────────────────┤
│  ┌───────────────────────────────┐  │
│  │ First clip preview text...    │  │ ← 24px row height
│  └───────────────────────────────┘  │ ← 8px padding
│  ┌───────────────────────────────┐  │
│  │ Second clip preview text...   │  │
│  └───────────────────────────────┘  │
└─────────────────────────────────────┘
```

**After (Target - Compact Utility):**
```
┌─────────────────────────────────────┐
│ [🔍]                                 │ ← 20px search box, no padding
├─────────────────────────────────────┤
│ First clip preview text...          │ ← 18px row height
│ Second clip preview text...         │ ← 1px separator
│ Third clip preview text...          │
│ Fourth clip preview text...         │
└─────────────────────────────────────┘
```

### Key Implementation Changes

1. **Window Style**
   - Change from `WindowStyle.SingleBorderWindow` to `WindowStyle.None`
   - Add thin 1px border manually
   - Custom title bar or no title bar
   - Remove drop shadow

2. **List Style**
   - Change ListView to flat-style ListBox
   - Row height: 18-20px (not 24px)
   - Font: 9pt Segoe UI (not 11-12pt)
   - ItemContainerStyle with minimal padding
   - No alternating row colors (or very subtle)

3. **Search Box Style**
   - Height: 20px (not 32px)
   - No border radius
   - Thin 1px border
   - Placeholder text in gray

4. **Paste Implementation**
   - Create `IPasteService` interface
   - Implement `PasteService` with Win32 SendInput
   - Set clipboard content → Send Ctrl+V to active window
   - Or use SendInput to type text directly

## Risks & Mitigation

### Risk 1: Paste to Active Window Fails
**Risk**: Target application might not accept paste correctly  
**Likelihood**: Medium  
**Impact**: High  
**Mitigation**: 
- Implement fallback strategies (SendKeys vs SendInput vs SetClipboard+Ctrl+V)
- Test with various applications (Notepad, VS Code, Chrome, Excel)
- Add retry logic with timeout

### Risk 2: UI Density Changes Break Readability
**Risk**: Making UI too compact might hurt usability  
**Likelihood**: Low  
**Impact**: Medium  
**Mitigation**:
- Follow Windows Explorer detail view as reference (18-20px rows work)
- Test with users who have vision impairments
- Make row height configurable in settings

### Risk 3: Window Positioning on Multi-Monitor
**Risk**: Popup might appear on wrong monitor  
**Likelihood**: Medium  
**Impact**: Low  
**Mitigation**:
- Get cursor position, show popup on same monitor
- Ensure popup stays within screen bounds
- Handle DPI scaling per monitor

## Development Phases

### Phase 1: Rename & Refactor (1-2 hours)
- [ ] Rename PowerPaste* → QuickAccess*
- [ ] Update all references in code
- [ ] Update DI registrations
- [ ] Update tests
- [ ] Update documentation

### Phase 2: Implement PasteService (2-3 hours)
- [ ] Create IPasteService interface
- [ ] Implement PasteService with Win32 APIs
- [ ] Get active window handle (GetForegroundWindow)
- [ ] Implement paste strategies:
  - Strategy 1: SetClipboard + SendInput(Ctrl+V)
  - Strategy 2: SendInput with text directly
  - Strategy 3: SendMessage with WM_PASTE
- [ ] Add unit tests for PasteService
- [ ] Integration tests with Notepad

### Phase 3: UI Density Redesign (3-4 hours)
- [ ] Update QuickAccessWindow.xaml styles
  - Window style: Remove chrome
  - Search box: 20px height, thin border
  - ListBox: Flat style, no padding
  - ListBoxItem: 18-20px height, minimal padding
  - Font: 9pt Segoe UI
  - Colors: System theme colors
- [ ] Test on different DPI settings
- [ ] Compare side-by-side with ClipMate 7.5 screenshots
- [ ] Test readability with longer clip previews

### Phase 4: Window Positioning (1-2 hours)
- [ ] Get cursor position (GetCursorPos)
- [ ] Calculate popup position near cursor
- [ ] Handle multi-monitor scenarios
- [ ] Keep popup within screen bounds
- [ ] Handle DPI scaling per monitor

### Phase 5: Polish & Testing (2-3 hours)
- [ ] Add keyboard shortcuts (Ctrl+1-9 for quick select?)
- [ ] Add clip type icons (text/image/file)
- [ ] Add clip source application name
- [ ] Test with 10+ applications
- [ ] Performance testing (popup time, search speed)
- [ ] Update user documentation

## Testing Strategy

### Unit Tests
- QuickAccessViewModel clip loading
- QuickAccessViewModel search filtering
- PasteService paste strategies
- Window positioning calculations

### Integration Tests
- Hotkey registration and triggering
- Popup show/hide behavior
- Paste to Notepad, VS Code, Chrome
- Multi-monitor positioning

### Manual Testing
- Test in 10+ applications
- Test on 100%, 150%, 200% DPI
- Test on multi-monitor setups
- Test with accessibility tools (screen readers)

## Documentation Requirements

- [ ] Update user guide with Quick Access feature
- [ ] Document keyboard shortcuts
- [ ] Add troubleshooting for paste failures
- [ ] Update architecture diagrams
- [ ] Reserve "PowerPaste" naming for future feature

## Open Questions

1. **Positioning**: Exact near-cursor or offset by fixed pixels?
2. **Clip Count**: 20 items or configurable?
3. **Search Scope**: Recent clips only or all clips?
4. **Icons**: Show clip type icons or pure text?
5. **Themes**: Support light/dark theme or just system theme?

## Definition of Done

- [ ] All success criteria met
- [ ] All phases completed
- [ ] 90%+ test coverage
- [ ] UI matches Windows utility style (compact, dense)
- [ ] Performance requirements met (<100ms popup, <50ms search)
- [ ] Tested on Windows 10 and Windows 11
- [ ] Tested on 100%, 150%, 200% DPI
- [ ] Documentation updated
- [ ] Code reviewed and committed
