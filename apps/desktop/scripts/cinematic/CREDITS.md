# Cinematic assets

UNSC crest: Merch UNSC Logo White.svg, traced by WyvernZu, uploaded by BaconShelf. Source: https://www.halopedia.org/File:Merch_UNSC_Logo_White.svg (original https://www.halopedia.org/images/2/21/Merch_UNSC_Logo_White.svg). Halo/UNSC imagery belongs to its respective rights holders. Personal fictional Halo-inspired project, not an official military or Microsoft application.

Motion graphics, timing and procedural geometry: scripts/cinematic/render.py. Native 3440 x 1440, 60 fps, 300 frames per clip. Logo rasterization through Chromium from the SVG at 800 x 1047 before native composition. No desktop wallpaper or static background screenshot is used as the scene.

Python render environment used: Pillow 11.3.0, NumPy 2.2.6. FFmpeg libx264 CRF 16 / medium / H.264 High / yuv420p, BT.709 tags; AAC 256 kbit/s. Render command: python scripts/cinematic/render.py. render-logo.cjs uses the repository Playwright installation and Chrome.

Audio is user-supplied Halo ODST/Virgil SFX. Since 0.1.12, startup.wav keeps the final five seconds of the original user-supplied Halo_Inizio.mp3 at natural speed, without atempo or trimming the ending; source gain 0.805 and a 40 ms fade-in, fixed app gain 0.8. shutdown.wav uses the audible section 27.65-35.65 seconds of the user's Halo_Fine.mp3, atempo 1.6 and the previously requested +15% gain, with limiter and fades. The old outro used an almost silent leading section. Both WAV sources are exactly five seconds. Only the outro uses time compression. Original user audio files are unchanged. App does not modify Windows audio settings.
