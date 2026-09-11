# YSO integration checks

`YsoIntegrationChecks.cs` is an Editor fixture for a **disposable** Unity 2022.3 project named `rogueslid-yso-tests`. Keep it outside this game's Assets: it writes synthetic PlayerPrefs and must not run against player/developer saves.

The validation project used here is `/private/tmp/rogueslid-yso-tests`. It contains the compiled runtime game/GameUtils/MAX sources, their runtime dependencies, and this fixture in `Assets/Editor`. It contains no publisher configuration or scene assets. The fixture exercises legacy JSON reads, backup recovery, collection normalization, audio preference propagation, background mute, and ad completion guards using synthetic state. It does not contact production YSO services or exercise native ads.

Run the fixture in that isolated project using Unity's `-batchmode -nographics -projectPath /private/tmp/rogueslid-yso-tests -executeMethod YsoIntegrationChecks.Run -logFile /private/tmp/rogueslid-yso-tests.log` arguments. The result marker is `YSO_CHECKS_PASSED`; failures exit with code 1.

A complete game Editor import, mobile build and on-device dashboard verification are still required before publishing.
