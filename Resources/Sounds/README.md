# Sound Files

This directory contains the default sound files for ClipMate sound events.

## Required Files

Place the following WAV/MP3 files in this directory:

1. **clipboard-update.wav** - Played when new clipboard data is captured
2. **append.wav** - Played when clips are appended together
3. **erase.wav** - Played when clips are erased/deleted
4. **filter.wav** - Played when clipboard data is filtered out
5. **ignore.wav** - Played when duplicate clipboard data is ignored
6. **powerpaste-complete.wav** - Played when PowerPaste operation completes

## File Specifications

- **Format**: WAV or MP3
- **Recommended Size**: 8-16KB per file
- **Sample Rate**: 22050 Hz or 44100 Hz
- **Bit Depth**: 16-bit
- **Channels**: Mono or Stereo

## Deployment

These files are automatically copied to the application's `Assets\Sounds\` directory during build.
The application looks for sound files at: `{AppDirectory}\Assets\Sounds\`

## Custom Sounds

Users can specify custom sound files via Options > Sounds tab. Custom sounds override the default sounds in this directory.
