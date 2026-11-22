# Local gameplay telemetry

Open **HighStakes > Analytics > Gameplay Sessions** in the Unity Editor. Click **Reload** after playing; select a session for its movement map. Browse can select an archived log directory. No service account, network connection or additional package is required.

In the Editor the default directory is `Application.persistentDataPath/Logs` (on this Windows installation: `%USERPROFILE%/AppData/LocalLow/Nutelka/hs_unity/Logs`). **HighStakes > Open Logs Folder** is also available through the existing log tools. In a standalone player the logger tries the game's Data directory, executable directory, persistent data directory, then a temporary fallback. The resolved location appears in the Console and diagnostic log header.

`Assets/Resources/GameLoggingConfig.json` enables diagnostic `.log` and structured `.jsonl` files by default. Firebase remains disabled. Gameplay payloads are always local, even if the optional diagnostic uploader is enabled. JSONL omits the legacy player identifier; session and event IDs are random local correlation IDs. Positions refer only to the fictional game map. Never add names, emails, account IDs or device identifiers to these payloads.

## Envelope and payload

Each JSONL line is an independent JSON object. Existing diagnostic fields remain: `timestamp`, `level`, `category`, `action`, `message`, optional `stackTrace`, `scene`, `sessionId`, and `build`. New records include `schemaVersion: 1` and `eventId`. Gameplay records have `category: "gameplay"`, a snake_case `action`, a structured `data` object, and `source` (`player`, `signal_validation`, or `interaction_validation`). Gameplay timestamps use ISO 8601 UTC. Older diagnostic timestamp strings remain supported.

`data` is a shared serialized field set: irrelevant fields have empty/zero defaults. Consumers must interpret fields according to the action, rather than treating every zero as a measurement. All durations are seconds; positions are Unity world metres and rotation is Euler degrees. Choices use a zero-based authored choice index plus its stable signal, avoiding localized text as an identifier. `objectiveId` is the authored step signal. `attempt` starts at one; retries retain the same mission ID.

| Action | Meaningful fields |
| --- | --- |
| `session_start` | Envelope session ID, timestamp, scene |
| `session_end` | `duration` (wall time), `completed` (a completed campaign was played or loaded in this session) |
| `mission_start` | `missionId`, `missionNumber`, `attempt`, envelope timestamp |
| `objective_complete` | `missionId`, `objectiveId`, `attempt`, `elapsed` (attempt time), `objectiveSeconds` |
| `choice_selected` | `dialogueId`, `choice`, `signal`, current `missionId` and `objectiveId` |
| `item_collected` | `itemId`, `hasPosition`, `position`, current mission/mode; emitted on successful story collection or inventory pickup |
| `police_alert` | `reason` (`alarm_call` or `visual_detection`), `position`, `hasPosition`, current mission/objective/attempt; one event per officer beginning a chase |
| `mission_failed` | `missionId`, `objectiveId`, `reason` (`timeout`, `caught`, `restart`), `elapsed`, `objectiveSeconds`, failed `attempt`, position |
| `mission_complete` | **Completed** `missionId` and `missionNumber`, `duration`, mission `score`, `totalScore`, `penalties`, `failures`, `mistakes`, `navigationSeconds`, `detections`, `efficiency`, `strategy` |
| `campaign_complete` | Final mission identity, `totalScore`, `rank`, campaign `duration`, `retries`, total `failures`/`mistakes`, `completed` |
| `player_position` | `hasPosition`, `position`, `rotation`, active gameplay `elapsed`, `mode`, mission/objective/attempt |
| `free_roam_start` | Current mission context if another briefing is available; empty mission ID after campaign completion |
| `campaign_restart` | Context before the New Campaign action resets the checkpoint |
| `validation_run` | `reason` identifies the automated validation type; these are executed tests, not human player study results |

Gameplay state transitions produce events only after the existing progression logic accepts them. Loading a completed save shows results without emitting another mission/campaign completion or giving another reward. Dialogue choices remain observable even if they decline a job, request unavailable information or answer a puzzle incorrectly.

Position sampling runs approximately once per active gameplay second, including Free Roam, and stops during conversation/input lock, pause, mixing UI, results and game over. Sampling does not run per frame. NPC detection is recorded by the actual police chase transition. Regular pickup events occur only after inventory admission succeeds. Optional telemetry failures are caught, payloads are serialized on the main thread, and disk I/O runs on the existing worker. Queue admission uses a short lock without disk I/O and atomic queue accounting shared by diagnostic and Console producers. Shutdown closes admission, drains accepted entries and emits one session end. A slow disk can outlive the bounded shutdown join; the worker retains ownership of final drain/close.

The existing file-size rotation, file-count retention and storage retention remain active. JSONL size also triggers paired rotation so structured payloads cannot grow an unlimited sidecar. A saturated bounded queue may drop events and emits `queue_overflow`; analytics from such sessions are partial.

## Interpreting the analytics

- Overview groups rotated parts by session ID and deduplicates event IDs. A session is completed if it has a campaign-complete event or a completed session end. Ended-session average duration uses wall time (including pause); incomplete sessions use the maximum observed active sampling time and are lower bounds.
- Mission time is the total active mission time across attempts. Scores and penalties come from the existing score model, rather than being recalculated in the analytics window. Failure frequency is recorded failures / recorded mission starts. Retry counts are observed starts with attempt > 1; interrupted/missing logs can undercount them.
- Objective time measures active time between accepted steps within an attempt, excluding dialogue and pause. Failed steps and incorrect choices are counted separately and sorted first. Navigation estimates active time beyond the current target's 2.7-metre interaction radius; it includes travel, detours and wandering, rather than inferring player intent.
- Efficiency is successful-attempt active time / all attempt active time, from 0 to 1. A first-attempt clear is 100%; this is a retry-overhead measure, not shortest-path efficiency. Older saved results without process measurements do not display the new player-facing summary.
- Strategy IDs are `locker_code` and `forced_locker`. The UI compares recorded successful outcomes; dialogue choice counts also retain unsuccessful attempts. An alarm on any attempt continues to forfeit the clean-run bonus, preserving existing scoring.
- The map is north-up, world X/Z, ±45 metres. Its sixteen building footprints exactly match `OldQuarterBootstrap.BuildDistrict`. Cyan lines are sampled movement; red points are failures; yellow points are officer detections. Long gaps and large teleports break paths. It is a footprint representation, not an invented heatmap or a fabricated terrain image.
- Validation runs and legacy/unknown sources are labeled in the session selector. Overview and performance totals include recorded validation runs. Select a separate directory when analyzing human-only studies. Test runs with arranged positions are unsuitable for navigation difficulty or human playtime conclusions.

Empty directories, missing optional fields, diagnostic-only legacy JSONL, malformed/truncated lines, duplicate event IDs and unreadable files are handled without failing the window. The window reports skipped lines and unreadable files. Retention or application crashes can remove lifecycle endpoints, so missing data must not be interpreted as successful completion.
