# Campaign validation

Validated on Windows 11, Unity **6000.6.6f1**, **22 November 2026**. The configured Unity version and package dependencies were preserved. Colleague commit `6d4f205` (logger recovery/disk verification) was integrated before the final checks.

## Executed checks

The Unity MCP status tool initially reported no Pipeline connection because the Editor was in Safe Mode. Fixing the logger test compilation errors restored the connection. Editor inspection, compilation, scene/runtime evaluation, input simulation and capture used Unity's recommended CLI against that connected Pipeline instance. Native Windows input was additionally used for the visible New Campaign button, packaging and pickup.

| Check | Actual result |
| --- | --- |
| Asset/script refresh and compilation | Passed: `script recompile_status` returned `completed`, `failed: false`, no errors. Existing obsolete-API/unused-field warnings remain in unrelated legacy code. |
| `OldQuarterTools.ValidateStory` | Passed: five missions, scoring, bonuses, penalties, retries, checkpoint restore, completed-state and duplicate-reward guards. |
| `GameLoggerTests.RunAllTests` | **28 assertions passed, zero failed**. Included actual 1 MB rotation, paired JSONL, actual retention cleanup preserving an unrelated file, 9,216 sequential events plus 2,048 concurrent events, the colleague's additional 5,000-event recovery check and 2,000 mixed concurrent/Console events, shutdown drain/idempotence, exclusive file-handle release and flush. |
| `GameplayAnalyticsTests.Run` | Passed: empty folder, malformed line, older diagnostics, duplicate IDs, structured payload/control-character roundtrip, optional localization fallbacks without Console errors, retry efficiency, navigation, detections and score penalties. Temporary fixtures are isolated from real log directories. A final Play Mode check also resolved a generated packaged-item name 100 times without errors. |
| `CampaignRuntimeChecks.StartInteractive` | Passed, stored exit code **0**, measured runtime errors **0**. Physical start/objective/completion log records on disk, four interiors, runtime NavMesh, objective approaches, locked/unlocked doors, five simulated missions, intermission/final results, Free Roam HUD/controls, pause, completed checkpoint and duplicate payout/failure guards. |
| `CampaignInteractionChecks.Start` | Passed through all five missions using queued keyboard/mouse input, normal E interactions, dialogue choices and real hold/search timers. Player approaches were arranged with teleports. Both warehouse approaches were exercised, including unavailable-code rejection, forced-locker alarm, actual police catch/retry, Ada's quiet route, puzzle mistake and final broadcast catch/retry. |
| Completed save scene reload | Passed: loaded final results, same score, five results, time scale 0 and locked controls; Continue Free Roam resumed normal controls. Loading did not emit another completion event. |
| New Campaign | Passed through the visible pause-menu button using native mouse input: mission index and score reset to zero, checkpoint removed, best score retained. |
| Free Roam inventory/production | Existing components remained present. Actual mixing method consumed supplied test ingredients and produced a mixed item. Native E input packaged it, consumed its bag, then collected the package into inventory. This is a production smoke test, not a full farming-cycle playthrough. |
| Pause sampling | Position record count stayed unchanged at 82 across the observed pause interval. |
| JSONL and shutdown | Parsed executed gameplay records with zero malformed lines in inspected files. Final merged-code session `19239aa7` contained five correctly numbered mission completions, three police alerts, two failures, exactly one campaign completion and one session end; its final gameplay event was `session_end`. |
| Analytics window | Opened and inspected using collected runtime JSONL, including overview, mission/objective/decision tables and per-session paths/failure/police markers on the district footprint map. Validation sources are labeled; these are test executions, not human study results. |
| Repository checks | `git diff --check`; English/Slovak added dialogue/result localization entries parsed and checked. |

The first runtime reachability run failed at the maintenance note because the old test only probed a 1.5 m circle. Actual gameplay approaches exist within the 2.7 m interaction range; the test now checks that range. Inspection also found a real unreachable safehouse hold pad behind the supply shelf. It was moved onto the reachable central aisle and both runtime and interaction runs subsequently exercised it.

One interaction rerun lost a synthetic puzzle key when the floating analytics window took Game view focus. The driver now focuses Game view before keys. For repeatable unattended runs, route Input System Editor input to Game view for the duration of the check and restore the original setting afterward. This changes only test input routing, not the game's bindings or progression. The failed rerun was not counted as a pass. The results-button check now selects the localized Free Roam button explicitly instead of relying on unordered object discovery; an earlier merged-code run completed the campaign but selected New Campaign and therefore was not counted as a complete interaction pass. The colleague's new on-disk runtime check also exposed Windows file-sharing restrictions in `File.ReadAllText` while the logger was open; it now reads through a `FileShare.ReadWrite` stream, matching the logger verification reader.

## Reproduce

- **HighStakes > Tests > Run GameLogger Verification Tests** runs the existing logger checks.
- **HighStakes > Tests > Run Gameplay Analytics Regression Tests** runs model and parser checks.
- **HighStakes > Tests > Run Campaign Runtime Checks** starts a fresh temporary campaign, runs signal simulation, then restores the checkpoint and best score. The existing batch entry point `CampaignRuntimeChecks.Start` still exits the Editor; the interactive menu does not.
- For interaction coverage, back up the checkpoint/best score, start a fresh OldQuarter campaign in Play Mode, focus Game view, and run **HighStakes > Tests > Run Interactions In Fresh Play Session**. It does not replace manual traversal; restore the backed-up preferences afterward.
- Open **HighStakes > Analytics > Gameplay Sessions**, select the collected session and click **Reload**. See [TELEMETRY.md](TELEMETRY.md) for paths, schema and metric definitions.

## Fixes and changed files

- `Assets/Editor/GameLoggerTests.cs`: correct reflection import/private-config access; real rotation/retention checks; queue and concurrency regressions.
- `Assets/Scripts/Telemetry/GameLogger.cs`, `GameLoggingConfig.cs`, and `Assets/Resources/GameLoggingConfig.json`: shared atomic queue accounting/admission, orderly shutdown and reload cleanup, structured local payloads, JSONL default/rotation, safe optional uploader dispatch.
- `Assets/Scripts/Telemetry/GameplayEventData.cs` (+ meta): shared structured payload schema.
- `Assets/Scripts/City/CityHud.cs`, `CityMissionDirector.cs`, `CityStory.cs`: null-safe completed HUD, correct completed-mission identity/measurements, guarded Free Roam failure handling, telemetry and process measurements, explicit locker choices, post-campaign responses, pickup-priority object comparison.
- `Assets/Scripts/City/OldQuarterBootstrap.cs`: reachable safehouse pad.
- `Assets/Scripts/Npc/PoliceBehavior.cs`, `Assets/Scripts/PickableObject.cs`: actual detection/successful pickup instrumentation.
- `Assets/Scripts/Localization/LocalizationManager.cs`: optional fallback lookup now avoids repeated missing-key Console errors for generated mixed/packaged item names and applies fallback replacements.
- `Assets/Scripts/PauseMenuController.cs`: localized New Campaign action accessible in Free Roam pause.
- `Assets/Resources/City/old_quarter_story.json`, `Assets/Resources/Localization/en.json`, `sk.json`: explained warehouse approaches, visible consequences and post-campaign NPC dialogue in both languages.
- `Assets/Editor/GameplayAnalyticsWindow.cs` (+ meta): local importer, metrics and accurate footprint movement map.
- `Assets/Editor/GameplayAnalyticsTests.cs`, `CampaignInteractionChecks.cs` (+ metas), `CampaignRuntimeChecks.cs`: regression and interaction coverage distinct from simulated progression.
- `README_KAMPAN_SK.md`, `TELEMETRY.md`, `CAMPAIGN_VALIDATION.md`: controls, Free Roam, analytics/schema and actual validation evidence.

## Limits

No standalone player build/export was produced. A complete manual walk through the city and a full planting/growing/watering/harvesting cycle were not performed. Interaction tests arrange positions, so their navigation times and playtimes must not be treated as human performance metrics. Timeout behavior was covered by the existing model tests, rather than waiting for every mission's full countdown. No human study or questionnaire was added. Existing legacy warnings and the repository's pre-existing missing original art/LFS assets were not broadly refactored.

Local capture artifacts from this session are under ignored `Logs/`: `validation-initial.png`, `validation-final-results.png`, `validation-free-roam.png`, and the final analytics capture. JSONL remains in the user's local log directory and is excluded from version control. The user's checkpoint and best score are restored after validation.
