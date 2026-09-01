# Cursor + Hera Unity context

From the project root, run:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\hera\capture-unity-context.ps1
```

The script writes a point-in-time snapshot to `artifacts/hera-cursor/`:

- `context.md`: concise human-readable summary
- `status.txt`: Hera connection status
- `scene.json`: active scene information
- `console-errors.json`: recent Unity Console errors
- `scene_view.png`: Scene View capture
- `game_view.png`: Game View capture
- `tools.json`: available Hera tools

Cursor Agent can read these files after running the script. Unity must be open with the Hera Connector loaded. If the status says that no Unity instances are found, open the project and run the script again.
