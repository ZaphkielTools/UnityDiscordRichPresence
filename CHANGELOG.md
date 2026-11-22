# Changelog

## [1.0.0](https://github.com/ZaphkielTools/UnityDiscordRichPresence/releases/tag/v1.0.0) (2026-11-22)

First release.

### Features

* Shows the active scene, the project name and the time since the editor started as your Discord activity, over the local IPC pipe of the Discord desktop app. No native plugin.
* Large image that follows the editor version: `unity-6` on Unity 6, `unity` on Unity 2022.3.
* Small image for VRChat projects, `vrc-avatar` or `vrc-world`, chosen from the installed VRChat SDK.
* Preferences page with the connection status, a preview of the fields sent to Discord, and per-project settings saved in `UserSettings/`.
* The activity stays visible during script compilation and when entering Play Mode.
* Updates are sent at most once every 4 seconds and only when a field changes. Text longer than Discord's 128 characters is shortened with an ellipsis.
