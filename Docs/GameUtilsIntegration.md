# GameUtils integration

GameUtils 2.19.1, MAX and External Dependency Manager were imported from the local riot-app reference. Rogueslid uses Unity 2022.3.62f2. No Riot Firebase configuration, advertising identifiers or build/signing scripts are included.

## Run lifecycle

- Home Play calls `GameServices.PlayAfterInterstitial`. The run starts in the SDK callback, including when no ad is available. Repeated clicks and repeated callbacks are guarded. The SDK retains control of its first-call skip and interval.
- `BoardManager.ResetArenaProgression` calls `GameServices.StartRun(CurrentTourmentLevel)`. A complete run is one game; advancing arenas does not increment the counter. Retry and direct startup both use this path.
- `UIGame.ShowWinMenu` calls `FinishRun(true)`; `ShowLoseMenu` calls `FinishRun(false)`. Duplicate screen calls are ignored.
- Returning home during an active run, or replacing an unfinished run, closes it as a loss. Quitting the application is not artificially recorded as a loss.
- `GameServices.GamesPlayed` reads the persistent GameUtils counter through `ADataManager`. `YCManager.OnGameStarted` increments it when the SDK is running. Before configuration, the integration increments that same key itself. It is saved immediately and does not require a second counter or later migration.
- The analytics level parameter is the selected Tourment (minimum 1), not the lifetime run count.

## Pending service configuration

`Assets/Resources/YCConfigData.asset` is the game's configuration, referenced by the SDK's existing Resources/YCManager prefab. While `gameYcId` is empty, the bootstrap skips online SDK initialization; Play and the local counter remain usable. Entering an ID requires restarting Play Mode/the application.

Before enabling services:

1. Set Rogueslid's YSO game ID and import its configuration via the YCConfig inspector.
2. Supply the correct MAX SDK key and Android/iOS ad unit and AdMob app IDs. AppLovinSettings is intentionally empty. Banners are disabled; the requested placement is the Home Play interstitial.
3. Install/configure Firebase with Rogueslid's own Android JSON and iOS plist if required by the publishing setup. Enable FIREBASE only after installing its dependencies. Facebook is optional behind YC_FACEBOOK in the runtime and editor assemblies; install its SDK before enabling that symbol in both assemblies. Tenjin is included but disabled in the pending configuration.
4. Resolve Android/iOS native dependencies and validate bundle IDs, mobile build settings and the SDK's platform build integration before producing release builds. The reference project's signing/build scripts were excluded.
5. Test consent/ATT where applicable, no-fill/offline behavior, ad-close callback, audio restoration, Win, Lose and Retry on a device. Confirm analytics start/end events and run counts in the dashboard.

## Local SDK adaptations

- YCBootstrapper skips unconfigured online services.
- Optional assembly references for IAP, notifications, Amazon and Google Play Review were removed because these packages are not installed. Restore appropriate references when adding those features.
- Newtonsoft 3.2.2 is declared in Packages/manifest.json. YC_NEWTONSOFT is enabled by versionDefines on the two GameUtils assemblies.
- Facebook imports/setup are guarded by YC_FACEBOOK.
- NiceVibrations BuildProfile code is guarded for Unity 6; Unity 2022 uses the existing PlayerSettings fallback.

Preserve these adaptations when updating GameUtils.

## Previous verification

The runtime sources and GameUtils/MAX editor sources compile with Unity 2022's Roslyn compiler and the project's existing references (warnings only). This was an isolated source compilation, not an Editor import or mobile build. The batch Editor check could not run because the project is already open, and live UI access was unavailable. Verify the package import in the open Editor before testing. SDK prefab references were checked against project/package metadata; the reference project's settings back icon was included to resolve its dependency.

## Audio and settings integration (2026-09-10)

- `YCConfigData.SoundEffect` and `SoundMusic` are enabled. The existing `BSettings` button opens the standard GameUtils window once the SDK is configured. While online initialization is disabled, the button is disabled.
- Rogueslid retains its serialized audio clips and sound lifetimes. Its `SoundManager` reads the public GameUtils sound settings and volumes; before SDK initialization it reads the same versioned `ADataManager` preferences. No clips need moving to `Resources/Sounds`.
- Music and victory/lose jingles follow Music. World effects, looping effects and UI sounds follow Effects. Changes also affect sources already playing, with independent base volumes and no repeated volume multiplication.
- Fullscreen ad requests mute all managed game audio and pause music until completion. Application pause uses the same suspension logic. The bridge never changes global `AudioListener` state. Ordinary gameplay time-scale pauses do not mute UI audio.
- AdsManager now completes interstitial/rewarded callbacks on display failure as well as close, clears callbacks before invocation, guards duplicate completion, and restores the previous time scale only if it actually paused it. Failed rewarded ads grant no reward.
- Preserve the AdsManager modifications when updating the vendor SDK.

## Existing player data

The original PlayerPrefs keys remain `rogueslid.character-progression` and `rogueslid.equipment-builds`. Do **not** replace these directly with `ADataManager.GetObject/SetObject`: ADataManager appends a version suffix and would read different keys. The original JSON schema is retained.

`GameSaveStore` reads the existing saves, keeps the previous valid JSON under `.backup`, and preserves malformed JSON under `.unreadable` for investigation before allowing new writes. It falls back to the backup if the primary JSON cannot be parsed. Recovery can lose the latest change if only the older backup is readable; it is not an atomic transaction or cloud backup. Missing/null collection fields are normalized so older saves remain usable. The explicit Clear Save action also removes its backup so cleared progress cannot reappear.

The installed GameUtils data layer is local PlayerPrefs storage. Analytics sends session/run/ad data; it does **not** upload the character progression or saved equipment builds and does not restore them on another device. No cloud account or cross-device save service has been added.

Before shipping an update for existing players, agree on the application identity and signing with YSO. The current Android identifier is `com.FeatherworksStudio.RogueSlide`; no explicit iOS identifier is serialized. Changing the bundle/application identifier can create a separate installation and prevent access to the previous local save. This integration does not migrate between different application identities, uninstallations or devices. Company/product settings have not been changed by this preparation.

## Publisher handoff

1. Log in to https://gameutils.ysocorp.com/ and create/select the correct Rogue Slide game. The portal has not yet been configured from this task because an authenticated session is required.
2. Confirm the final Android/iOS bundle IDs before importing the game's own configuration. Enter its ID in `YCConfigData` and use **Import Config**. Never reuse the reference game's credentials.
3. Run **Tools > Rogue Slide > YSO > Check Configuration** to inspect missing local fields and the prefab reference. This is a diagnostic, not certification of a release build.
4. Complete platform services, consent and mediation requirements with YSO, then restart the game to initialize the SDK.
5. On a device, verify Home Play, Win, Lose, Retry, background/resume in a run, repeated ad callbacks, no-fill/display failure, audio settings persistence and an update over an existing installed save. Confirm sessions, run outcomes, playtime and ad revenue on the correct YSO dashboard.

The installed RequestManager sends analytics sessions on pause/quit without a persistent retry queue. Offline delivery and abrupt process termination are not guaranteed. Use YSO's supported SDK/version and validate this requirement with their team before claiming lossless telemetry. The run-to-session behavior after background/resume also requires device/dashboard validation.

## Validation of this preparation

- Runtime game/GameUtils/MAX sources and SDK editor sources, including the new configuration validator, compile with Unity 2022.3.62f2's Roslyn compiler (warnings only).
- 16 synthetic checks passed in a disposable Unity 2022.3.62f2 project: original JSON keys, save round trips, malformed JSON backup recovery/preservation, clear behavior, legacy character data, null collections, effect volume/mute propagation, application-pause mute/resume and independent jingle/music settings.
- The expanded ad-callback fixture is retained in `Tools/Validation/YsoIntegrationChecks.cs`, but its subsequent Editor launch stalled while initializing platform modules, before test execution. Its ad assertions are therefore **not reported as passed**. Both stalled validation processes were stopped.
- This does not validate audible playback timing, the complete game's scene import, native mobile SDKs, actual ad callbacks, consent, or dashboard delivery. Only another Unity project was connected to MCP, so its editor was left untouched.
