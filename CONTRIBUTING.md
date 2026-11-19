# Contributing

Thanks for helping. Most contributions are new entries in the tweak catalog.

## Add a tweak

Edit `src/Limpio.Core/Catalog/tweaks.json` and add an object to `tweaks`:

```json
{
  "id": "ads.advertising-id",
  "category": "ads",
  "title": { "es": "ID de publicidad", "en": "Advertising ID" },
  "description": { "es": "Desactiva el identificador de publicidad.", "en": "Turns off the advertising identifier." },
  "note": { "es": "Opcional.", "en": "Optional." },
  "risk": "safe",
  "presets": ["minimal", "recommended", "aggressive"],
  "actions": [
    { "type": "registry", "hive": "HKCU", "path": "Software\\Microsoft\\Windows\\CurrentVersion\\AdvertisingInfo",
      "name": "Enabled", "valueType": "dword", "value": 0 }
  ]
}
```

Fields:

- `id`: unique, `category.short-name`.
- `category`: `apps`, `telemetry`, `privacy`, `ai`, `ads`, `services`, `scheduledTasks`.
- `title`, `description`: both `es` and `en` are required. `note` is optional (warnings such as "keep it if you play on PC").
- `risk`: `safe`, `moderate` or `advanced`.
- `presets`: any of `minimal`, `recommended`, `aggressive`. Presets are nested: minimal implies recommended implies aggressive, and advanced tweaks cannot be in recommended. Use `[]` for custom-only.
- `actions`: one or more of:

| type | fields |
|---|---|
| `appx` | `packageName` (wildcards allowed, for example `*McAfee*`) |
| `service` | `name`, `startType` (`Disabled`, `Manual`, `Automatic`) |
| `scheduledTask` | `path` (for example `\\Microsoft\\Windows\\...\\TaskName`) |
| `registry` | `hive` (`HKLM` or `HKCU`), `path`, `name`, `valueType` (`dword`, `qword`, `string`), `value` |
| `win32Uninstall` | `displayName` (uninstall entry), `processName`, `uninstallers` (paths, env vars and wildcards allowed), `uninstallArgs`, `runValueName`, `protectKnownFolders`, `revertHint` |

Every action can detect its state, apply itself and record the original value for revert. Do not add targets from the protected list (`src/Limpio.Core/Protected.cs`): the catalog test fails and the engine refuses them anyway.

## Check your change

```
dotnet test
```

The tests validate the catalog (unique ids, valid categories and presets, es and en text, no protected targets). Then regenerate the table in `docs/tweaks.md`:

```
LIMPIO_UPDATE_DOCS=1 dotnet test
```

On PowerShell: `$env:LIMPIO_UPDATE_DOCS = "1"; dotnet test`.

## Rules

- Never run apply or revert code against your own machine from tests. Use the fakes in `tests/Limpio.Core.Tests/Fakes.cs`.
- Keep logic in `Limpio.Core` behind interfaces; the WPF project only holds UI.
- UI text lives in `src/Limpio/Services/Strings.cs` in Spanish and English.
