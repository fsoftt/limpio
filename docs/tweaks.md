# Tweak catalog

Generated from `src/Limpio.Core/Catalog/tweaks.json`. Do not edit by hand: run
`LIMPIO_UPDATE_DOCS=1 dotnet test` to regenerate it.

60 tweaks.

## Apps

| Id | Title | Risk | Presets | Targets |
|---|---|---|---|---|
| `apps.bing-news` | Bing News | safe | minimal, recommended, aggressive | appx: `Microsoft.BingNews` |
| `apps.bing-weather` | Bing Weather | safe | minimal, recommended, aggressive | appx: `Microsoft.BingWeather` |
| `apps.bing-search` | Bing Search | safe | recommended, aggressive | appx: `Microsoft.BingSearch` |
| `apps.start-experiences` | Start Experiences | safe | recommended, aggressive | appx: `Microsoft.StartExperiencesApp` |
| `apps.solitaire` | Solitaire Collection | safe | minimal, recommended, aggressive | appx: `Microsoft.MicrosoftSolitaireCollection` |
| `apps.clipchamp` | Clipchamp | safe | minimal, recommended, aggressive | appx: `Clipchamp.Clipchamp` |
| `apps.power-automate` | Power Automate | safe | recommended, aggressive | appx: `Microsoft.PowerAutomateDesktop` |
| `apps.todo` | Microsoft To Do | safe | recommended, aggressive | appx: `Microsoft.Todos` |
| `apps.get-help` | Get Help | safe | recommended, aggressive | appx: `Microsoft.GetHelp` |
| `apps.feedback-hub` | Feedback Hub | safe | minimal, recommended, aggressive | appx: `Microsoft.WindowsFeedbackHub` |
| `apps.groove` | Groove Music (Zune) | moderate | aggressive | appx: `Microsoft.ZuneMusic` |
| `apps.office-hub` | Microsoft 365 (Office Hub) | safe | minimal, recommended, aggressive | appx: `Microsoft.MicrosoftOfficeHub` |
| `apps.new-outlook` | New Outlook | safe | recommended, aggressive | appx: `Microsoft.OutlookForWindows` |
| `apps.teams` | Microsoft Teams (personal) | safe | recommended, aggressive | appx: `MicrosoftTeams`<br>appx: `MSTeams` |
| `apps.cortana` | Cortana | safe | minimal, recommended, aggressive | appx: `Microsoft.549981C3F5F10` |
| `apps.copilot-app` | Copilot (app) | safe | recommended, aggressive | appx: `Microsoft.Copilot` |
| `apps.xbox` | Xbox and gaming services | moderate | aggressive | appx: `Microsoft.GamingApp`<br>appx: `Microsoft.XboxGamingOverlay`<br>appx: `Microsoft.XboxSpeechToTextOverlay`<br>appx: `Microsoft.Xbox.TCUI`<br>appx: `Microsoft.XboxIdentityProvider` |
| `apps.phone-link` | Phone Link | moderate | aggressive | appx: `Microsoft.YourPhone`<br>appx: `MicrosoftWindows.CrossDevice` |
| `apps.widgets` | Widgets | moderate | recommended, aggressive | appx: `MicrosoftWindows.Client.WebExperience`<br>registry: `HKLM\SOFTWARE\Policies\Microsoft\Dsh\AllowNewsAndInterests` |
| `apps.sticky-notes` | Sticky Notes | moderate | custom only | appx: `Microsoft.MicrosoftStickyNotes` |
| `apps.skype` | Skype | safe | recommended, aggressive | appx: `Microsoft.SkypeApp` |
| `apps.legacy-misc` | People and Mixed Reality | safe | recommended, aggressive | appx: `Microsoft.People`<br>appx: `Microsoft.MixedReality.Portal`<br>appx: `Microsoft.Microsoft3DViewer`<br>appx: `Microsoft.Print3D` |
| `apps.tips` | Quick Assist and Tips | moderate | aggressive | appx: `MicrosoftCorporationII.QuickAssist`<br>appx: `Microsoft.Getstarted` |
| `apps.onedrive` | Uninstall OneDrive | moderate | aggressive | win32Uninstall: `Microsoft OneDrive` |
| `apps.oem-mcafee` | McAfee (preinstalled) | safe | aggressive | appx: `*McAfee*` |
| `apps.oem-norton` | Norton (preinstalled) | safe | aggressive | appx: `*Norton*` |
| `apps.oem-wildtangent` | WildTangent Games (preinstalled) | safe | aggressive | appx: `*WildTangent*` |
| `apps.oem-booking` | Booking.com (preinstalled) | safe | aggressive | appx: `*Booking*` |
| `apps.oem-spotify` | Spotify (preinstalled) | safe | aggressive | appx: `SpotifyAB.SpotifyMusic` |
| `apps.oem-disney` | Disney+ (preinstalled) | safe | aggressive | appx: `*Disney*` |
| `apps.oem-tiktok` | TikTok (preinstalled) | safe | aggressive | appx: `*TikTok*` |
| `apps.oem-instagram` | Instagram (preinstalled) | safe | aggressive | appx: `*Instagram*` |
| `apps.oem-facebook` | Facebook (preinstalled) | safe | aggressive | appx: `*Facebook*` |
| `apps.oem-candycrush` | Candy Crush (preinstalled) | safe | aggressive | appx: `*CandyCrush*` |
| `apps.oem-amazon` | Amazon (Prime, Alexa) (preinstalled) | moderate | aggressive | appx: `*Amazon*` |
| `apps.oem-netflix` | Netflix (preinstalled) | safe | aggressive | appx: `*Netflix*` |

## Telemetry

| Id | Title | Risk | Presets | Targets |
|---|---|---|---|---|
| `telemetry.policy` | Minimal telemetry (policy) | safe | minimal, recommended, aggressive | registry: `HKLM\SOFTWARE\Policies\Microsoft\Windows\DataCollection\AllowTelemetry` |
| `telemetry.diagtrack` | DiagTrack service | safe | minimal, recommended, aggressive | service: `DiagTrack` |
| `telemetry.dmwappush` | dmwappushservice | safe | minimal, recommended, aggressive | service: `dmwappushservice` |
| `telemetry.feedback` | Feedback frequency | safe | minimal, recommended, aggressive | registry: `HKCU\Software\Microsoft\Siuf\Rules\NumberOfSIUFInPeriod` |
| `telemetry.wer` | Windows Error Reporting | moderate | recommended, aggressive | service: `WerSvc` |

## Privacy

| Id | Title | Risk | Presets | Targets |
|---|---|---|---|---|
| `gaming.gamebar` | Game Bar and capture | moderate | aggressive | registry: `HKCU\Software\Microsoft\Windows\CurrentVersion\GameDVR\AppCaptureEnabled`<br>registry: `HKCU\System\GameConfigStore\GameDVR_Enabled` |
| `privacy.bing-search` | Bing in Start search | safe | minimal, recommended, aggressive | registry: `HKCU\Software\Policies\Microsoft\Windows\Explorer\DisableSearchBoxSuggestions` |
| `privacy.activity-history` | Activity history | safe | recommended, aggressive | registry: `HKLM\SOFTWARE\Policies\Microsoft\Windows\System\PublishUserActivities`<br>registry: `HKLM\SOFTWARE\Policies\Microsoft\Windows\System\UploadUserActivities` |
| `privacy.location` | Location services | advanced | aggressive | registry: `HKLM\SOFTWARE\Policies\Microsoft\Windows\LocationAndSensors\DisableLocation` |

## AI

| Id | Title | Risk | Presets | Targets |
|---|---|---|---|---|
| `ai.copilot-policy` | Turn off Copilot | safe | minimal, recommended, aggressive | registry: `HKCU\Software\Policies\Microsoft\Windows\WindowsCopilot\TurnOffWindowsCopilot`<br>registry: `HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot\TurnOffWindowsCopilot` |
| `ai.recall` | Turn off Recall | safe | minimal, recommended, aggressive | registry: `HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsAI\DisableAIDataAnalysis`<br>registry: `HKCU\Software\Policies\Microsoft\Windows\WindowsAI\DisableAIDataAnalysis` |
| `ai.copilot-button` | Hide Copilot button | safe | recommended, aggressive | registry: `HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced\ShowCopilotButton` |

## Ads

| Id | Title | Risk | Presets | Targets |
|---|---|---|---|---|
| `ads.advertising-id` | Advertising ID | safe | minimal, recommended, aggressive | registry: `HKCU\Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo\Enabled` |
| `ads.tailored` | Tailored experiences | safe | minimal, recommended, aggressive | registry: `HKCU\Software\Microsoft\Windows\CurrentVersion\Privacy\TailoredExperiencesWithDiagnosticDataEnabled` |
| `ads.content-delivery` | Suggestions and promoted apps | safe | minimal, recommended, aggressive | registry: `HKCU\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager\SilentInstalledAppsEnabled`<br>registry: `HKCU\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager\SubscribedContent-338388Enabled`<br>registry: `HKCU\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager\SubscribedContent-338389Enabled`<br>registry: `HKCU\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager\SubscribedContent-353694Enabled`<br>registry: `HKCU\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager\SubscribedContent-353696Enabled`<br>registry: `HKCU\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager\SystemPaneSuggestionsEnabled`<br>registry: `HKCU\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager\RotatingLockScreenOverlayEnabled`<br>registry: `HKCU\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager\SoftLandingEnabled` |
| `ads.start-recommendations` | Start recommendations | safe | minimal, recommended, aggressive | registry: `HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced\Start_IrisRecommendations` |

## Services

| Id | Title | Risk | Presets | Targets |
|---|---|---|---|---|
| `services.maps` | Downloaded maps | moderate | recommended, aggressive | service: `MapsBroker` |
| `services.retaildemo` | Retail demo | moderate | recommended, aggressive | service: `RetailDemo` |
| `services.xbox` | Xbox services | moderate | aggressive | service: `XblAuthManager`<br>service: `XblGameSave`<br>service: `XboxGipSvc`<br>service: `XboxNetApiSvc` |

## ScheduledTasks

| Id | Title | Risk | Presets | Targets |
|---|---|---|---|---|
| `tasks.ceip-consolidator` | CEIP Consolidator | safe | minimal, recommended, aggressive | scheduledTask: `\Microsoft\Windows\Customer Experience Improvement Program\Consolidator` |
| `tasks.ceip-usb` | CEIP UsbCeip | safe | minimal, recommended, aggressive | scheduledTask: `\Microsoft\Windows\Customer Experience Improvement Program\UsbCeip` |
| `tasks.appraiser` | Application Experience Appraiser | moderate | recommended, aggressive | scheduledTask: `\Microsoft\Windows\Application Experience\Microsoft Compatibility Appraiser Exp` |
| `tasks.marebackup` | MareBackup | moderate | recommended, aggressive | scheduledTask: `\Microsoft\Windows\Application Experience\MareBackup` |
| `tasks.pcapatch` | PcaPatchDbTask | moderate | recommended, aggressive | scheduledTask: `\Microsoft\Windows\Application Experience\PcaPatchDbTask` |
