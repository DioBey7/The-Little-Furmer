# Releasing the web build on itch.io

## 1. Build
1. Unity 2022.3 with the **WebGL Build Support** module installed (Unity Hub > Installs > Add modules).
2. Open the project and run **Tools > Build > Web build for itch.io**.
   The first WebGL build takes a while (often 10+ minutes); later ones are faster.
3. The result is `Builds/TheLittleFurmer-web.zip` (the folder `Builds/WebGL-itch/` holds the same files).

The menu item applies the itch.io settings for you: Gzip compression with
decompression fallback, the `Itch` page template, and size-optimized IL2CPP code.

## 2. Test locally first
Opening `index.html` straight from disk doesn't work in most browsers. Serve the folder instead, e.g.
`python -m http.server 8000` inside `Builds/WebGL-itch`, then open http://localhost:8000.

## 3. Upload to itch.io
On the project's **Edit game** page:
- **Kind of project:** HTML
- Upload the zip and tick **This file will be played in the browser**
- **Embed options > Viewport dimensions:** 960 x 540 (matches Player Settings > Resolution > Default Canvas Width/Height)
- Tick **Mobile friendly** (orientation: Landscape) and **Fullscreen button**
- Leave **SharedArrayBuffer support** off (the build doesn't use threads)

## Notes
- Saves on the web are kept in the browser's localStorage under `TheLittleFurmer/…`, so
  they survive uploading a new build. Players lose them if they clear site data or play in a private window.
- To change the game's aspect ratio, change Default Canvas Width/Height in Player Settings and
  the itch.io viewport to the same numbers; the page template letterboxes to that ratio.
