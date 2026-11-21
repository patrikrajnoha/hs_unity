# HighStakes Local File-Based Gameplay Logging

Production-ready, offline-first local file logging system for HighStakes (`hs_unity`).

All gameplay events, diagnostics, Unity errors, and unhandled exceptions are written to physical log files on the player's local machine without requiring Firebase, user authentication, or any network connection.

---

## Table of Contents

1. [Overview & Architecture](#overview--architecture)
2. [Log File Locations & Resolution Strategy](#log-file-locations--resolution-strategy)
3. [Log File Format & Examples](#log-file-format--examples)
4. [Logging API & Usage](#logging-api--usage)
5. [Configuration](#configuration)
6. [Unity Console & Exception Capture](#unity-console--exception-capture)
7. [Log Rotation & Retention Policy](#log-rotation--retention-policy)
8. [Offline Behavior & Security](#offline-behavior--security)
9. [Windows Standalone Behavior](#windows-standalone-behavior)
10. [Developer Utilities & Editor Tools](#developer-utilities--editor-tools)
11. [Troubleshooting](#troubleshooting)

---

## Overview & Architecture

The logging system is built around `GameLogger`, a persistent Unity singleton automatically initialized before the first gameplay scene loads via `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]`.

Key characteristics:

- **Asynchronous & Non-blocking:** Log events are enqueued into a thread-safe `ConcurrentQueue<LogEntry>`. A dedicated background writer thread (`GameLogger-Writer`) drains the queue in batches and writes them to disk.
- **Frame-rate Stable:** Zero gameplay hitches or frame spikes from disk I/O.
- **Immediate Error Flushing:** `ERROR` and `CRITICAL` events immediately wake the writer worker, ensuring crash diagnostics reach disk immediately.
- **Graceful Shutdown:** Bounded 2-second queue drain on application quit or process termination ensures no events are lost.
- **Thread Safety:** Full thread-safety across background workers, job threads, and Unity callbacks. No main-thread Unity APIs are invoked on background threads.
- **Backward Compatibility:** Legacy `FirebaseGameLogger` is retained as a zero-overhead compatibility facade forwarding all calls to `GameLogger`.

---

## Log File Locations & Resolution Strategy

Log files are stored in a designated `Logs` directory resolved automatically at startup:

### Resolution Strategy

1. **Unity Editor:**
   - Defaults to `Application.persistentDataPath/Logs` (`%USERPROFILE%\AppData\LocalLow\<CompanyName>\<ProductName>\Logs` on Windows) to prevent temporary log files from dirtying version-controlled project folders.
2. **Windows Standalone Builds:**
   - **Tier 1 (Preferred):** `<GameName>_Data/Logs/` inside the build directory.
   - **Tier 2:** `<ExecutableDirectory>/Logs/` beside the game `.exe`.
   - Before accepting either directory, `GameLogger` executes a write-probe test (creating and deleting a temporary probe file) to verify write permissions.
   - **Tier 3 (Fallback):** If the installation directory is read-only (e.g. installed under `C:\Program Files\` or protected Steam library folders without administrative privileges), `GameLogger` automatically falls back to `Path.Combine(Application.persistentDataPath, "Logs")`.
   - **Tier 4 (Ultimate Fallback):** System temporary directory (`%TEMP%\HighStakes\Logs`).
3. **Resilience:**
   - Logging never crashes the game or requests administrative privileges under any circumstances.
   - The resolved absolute log directory is printed once to the Unity console during initialization:
     ```text
     [GameLogger] Resolved log directory: C:\Users\<User>\AppData\LocalLow\DefaultCompany\hs_unity\Logs
     ```

---

## Log File Format & Examples

Each game session creates a dedicated UTF-8 log file:

`game_<yyyy-MM-dd_HH-mm-ss>_<sessionId>.log`

### Session Header

Every log file starts with a readable session header:

```text
==================================================
HIGHSTAKES GAME LOG
==================================================
Started UTC: 2026-11-21T21:30:15Z
Game version: 1.0.0
Unity version: 6000.6.6f1
Platform: WindowsPlayer
Session ID: 3e8392a7
Log directory: C:\Games\HighStakes\HighStakes_Data\Logs
==================================================
```

### Log Event Syntax

```text
[<Timestamp UTC with ms precision>] [<LEVEL>] [<CATEGORY>] <action> | <message>
```

When an exception or error includes a stack trace, it is cleanly indented directly beneath the event:

```text
[2026-11-21 21:30:18.145 UTC] [INFO] [GAME] game_started | Game initialized - Version: 1.0.0, Unity: 6000.6.6f1, Platform: WindowsPlayer
[2026-11-21 21:30:21.420 UTC] [INFO] [INVENTORY] item_added | Added Green Bud Seed (x1)
[2026-11-21 21:31:05.110 UTC] [INFO] [FARMING] seed_planted | Planted seed in Pot_01
[2026-11-21 21:31:12.350 UTC] [INFO] [FARMING] plant_watered | Watered Pot_01
[2026-11-21 21:31:40.230 UTC] [WARNING] [INVENTORY] inventory_full | Could not add Weed
[2026-11-21 21:31:55.670 UTC] [INFO] [ECONOMY] sale_completed | Sold 2 item(s) for $250
[2026-11-21 21:32:01.500 UTC] [ERROR] [UNITY] exception | NullReferenceException: Object reference not set to an instance of an object
Stack trace:
  at HighStakes.City.CityMissionDirector.Update () [0x00042] in Assets/Scripts/City/CityMissionDirector.cs:85
```

### Optional JSONL Output

When `enableJsonlLogging` is enabled in configuration, a companion `.jsonl` file is written alongside the `.log` file:

```json
{"timestamp":"2026-11-21 21:30:18.145","level":"INFO","category":"GAME","action":"game_started","message":"Game initialized","scene":"Menu","sessionId":"3e8392a7","playerId":"anonymous","build":"1.0.0"}
```

---

## Logging API & Usage

### Basic Methods

```csharp
// Debug logging (verbose diagnostics)
GameLogger.LogDebug("inventory", "items_reordered", "Moved slot 0 to slot 3");

// Informational events (gameplay actions, milestones, state changes)
GameLogger.LogInfo("farming", "seed_planted", "Planted Green Bud Seed in Pot_02");

// Warnings (recoverable issues, inventory full, failed interactions)
GameLogger.LogWarning("inventory", "inventory_full", "Cannot pick up item: bag full");

// Errors (failed operations, missing assets, broken interactions)
GameLogger.LogError("story", "missing_conversation", "Conversation node 'deal_end' not found");

// Critical & Exceptions (unhandled failures, fatal crashes)
GameLogger.LogCritical("system", "fatal_error", "Core database connection lost");
GameLogger.LogException(exception, "campaign", "save_failed", "Failed saving checkpoint");
```

### Direct Generic Method

```csharp
GameLogger.Log(GameLogLevel.Info, "economy", "item_bought", "Bought Seed for $50");
```

### Backward Compatibility

Existing calls to `FirebaseGameLogger` remain 100% functional without modification:

```csharp
FirebaseGameLogger.LogInfo("economy", "sale_completed", "Sold items");
```

---

## Configuration

Configuration is loaded automatically from `Assets/Resources/GameLoggingConfig.json` (or hardcoded defaults if missing).

Default configuration:

```json
{
  "enableFileLogging": true,
  "enableUnityConsoleLogging": true,
  "captureUnityErrors": true,
  "minimumLogLevel": 20,
  "maxFileSizeMB": 10,
  "maxLogFiles": 10,
  "maxStorageMB": 100,
  "flushIntervalSeconds": 2.0,
  "enableFirebaseLogging": false,
  "enableJsonlLogging": false
}
```

### Options Explained

| Setting | Type | Default | Description |
|---|---|---|---|
| `enableFileLogging` | bool | `true` | Writes logs to local disk files. |
| `enableUnityConsoleLogging` | bool | `true` | Echoes `GameLogger` messages to the Unity console. |
| `captureUnityErrors` | bool | `true` | Captures `Debug.LogError`, `Debug.LogWarning`, assertions, and exceptions. |
| `minimumLogLevel` | int | `20` (Info) | 10 = Debug, 20 = Info, 30 = Warning, 40 = Error, 50 = Critical. |
| `maxFileSizeMB` | int | `10` | Log file size limit in MB before rotating to part 2. |
| `maxLogFiles` | int | `10` | Maximum historical session log files retained. |
| `maxStorageMB` | int | `100` | Maximum total MB storage for logs in directory. |
| `flushIntervalSeconds` | float | `2.0` | Background disk flush interval. |
| `enableFirebaseLogging` | bool | `false` | Completely offline by default. Set `true` only for Firestore upload. |
| `enableJsonlLogging` | bool | `false` | Enables secondary `.jsonl` log file output. |

---

## Unity Console & Exception Capture

The logger subscribes to `Application.logMessageReceivedThreaded` to automatically capture:

- `Debug.LogWarning`
- `Debug.LogError`
- `Debug.LogAssert`
- `Debug.LogException`
- Unhandled script exceptions and engine asserts

### Anti-Recursion & Duplicate Protection

- When `GameLogger` echoes events to the Unity console (`Debug.Log*`), it activates a thread-local re-entrancy flag (`isInternalLogging`).
- Messages starting with `[GameLog]` or `[GameLogger]` are discarded by the Unity callback receiver.
- This guarantees zero recursion, zero duplicate entries, and zero stack overflows.

---

## Log Rotation & Retention Policy

To prevent unbounded disk usage on player computers:

1. **Size-based Rotation:**
   - When an active session log file reaches `maxFileSizeMB` (default 10 MB), it is cleanly flushed and closed.
   - The session continues in a new numbered file:
     `game_<timestamp>_<sessionId>_02.log`
   - A fresh header is written to the new file segment.
2. **Historical Cleanup:**
   - At startup and on rotation, `GameLogger` scans the log directory for matching session logs (`game_*.log`, `game_*.jsonl`).
   - If total matching files exceed `maxLogFiles` (default 10) or total size exceeds `maxStorageMB` (default 100 MB), the oldest files are safely deleted.
   - The currently active session log file is strictly protected from deletion.
   - Non-matching files (e.g., user documents or Unity `Editor.log`) are never touched.

---

## Offline Behavior & Security

- **100% Offline:** `GameLogger` requires no internet connection, DNS resolution, sockets, or HTTP endpoints.
- **Privacy First:** No authentication tokens, passwords, personal emails, or sensitive player credentials are logged.
- **Network Agnostic:** If the player has no network or firewall blocks all outbound traffic, logging functions identically.

---

## Windows Standalone Behavior

- On Windows desktop standalone builds, `GameLogger` prefers `<GameName>_Data/Logs/` or the directory beside the executable.
- If running from a restricted location (e.g. Steam library in `C:\Program Files (x86)\Steam\steamapps\common\...` under a non-administrator account), the probe detects the permission failure and silently falls back to `%USERPROFILE%\AppData\LocalLow\<CompanyName>\<ProductName>\Logs`.
- No UAC elevation prompts or administrator permissions are required.

---

## Developer Utilities & Editor Tools

### In Unity Editor

Use the top menu:

- **Tools > HighStakes > Open Logs Folder**
- **HighStakes > Open Logs Folder**

This immediately opens Windows Explorer / file manager to the active logs directory.

### In Code

```csharp
// Get the current resolved directory
string logDir = GameLogger.LogDirectory;

// Get the path of the active log file
string currentFile = GameLogger.CurrentLogFilePath;

// Open the log directory in the OS file explorer
GameLogger.OpenLogFolder();

// Force an immediate synchronous flush
GameLogger.Flush();
```

---

## Troubleshooting

### Q: Logs are not appearing in my game directory in the Unity Editor?
**A:** In the Unity Editor, `GameLogger` writes to `Application.persistentDataPath/Logs` by design so temporary run files do not pollute the Git repository. Use **Tools > HighStakes > Open Logs Folder** to inspect them.

### Q: Why are my Debug.Log messages not in the file?
**A:** By default, `minimumLogLevel` is set to `20` (Info) and Unity console capture only captures Warnings, Errors, Asserts, and Exceptions to avoid filling the log with high-volume development prints. Use `GameLogger.LogInfo(...)` for gameplay actions, or adjust `minimumLogLevel` in `Resources/GameLoggingConfig.json`.

### Q: Does logging continue during scene loads?
**A:** Yes. `GameLogger` is registered with `DontDestroyOnLoad` and subscribes to `SceneManager.sceneLoaded` to log scene transitions automatically.
