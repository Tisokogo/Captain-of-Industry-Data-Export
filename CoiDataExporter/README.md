# COI Data Exporter

Eine eigenständige Export-Mod für **Captain of Industry**.

## Zielbild

Für die spätere Benutzung ist **keine Installation außerhalb des Spiels** erforderlich:

1. ZIP herunterladen
2. ZIP nach `%APPDATA%\Captain of Industry\Mods` entpacken
3. Spiel starten
4. Mod aktivieren
5. Spielstand laden oder neues Spiel starten
6. JSON-Dateien liegen im Mod-Ordner bereit

Das externe CLI in diesem Repository ist nur ein optionales Entwicklerwerkzeug. Es wird zum Spielen und zum Erzeugen der JSON-Dateien nicht benötigt.

## Architektur

Captain of Industry baut die Prototypes zur Laufzeit innerhalb des Spiels auf. Deshalb liest die Mod die Daten aus `ProtosDb` und schreibt fertige Dateien. Es gibt zur Laufzeit keinen externen Prozess und keine Netzwerkverbindung.

```text
Captain of Industry
        │
        │ CoiDataExporter.Mod.dll
        ▼
%APPDATA%\Captain of Industry\Mods\coi-data-exporter\export\
        ├── coi-data.json
        ├── products.json
        ├── recipes.json
        ├── buildings.json
        └── export-status.json
```

Die Mod exportiert bei jeder Spielinitialisierung. Das umfasst auch das Laden eines bestehenden Spielstands. Die Daten sind statische Spieldaten; sie hängen nicht von der Fabrik im konkreten Save ab.

## Exportumfang – Version 0.1

- Produkte
- Rezepte mit Eingängen, Ausgängen und Dauer
- statische Entitäten/Gebäude
- Baukosten und Wartung
- Stromverbrauch/-erzeugung
- Lagerkapazität und Transfergeschwindigkeit, soweit die aktuelle Spielversion diese Felder bereitstellt
- versioniertes JSON-Schema
- separate JSON-Dateien für eine einfache Weiterverwendung

Noch nicht enthalten: Icons, Terrain, Fahrzeuge, Schiffs-Upgrades, Verträge und eine Weboberfläche.

## Dateien für die Benutzung

Nach erfolgreichem Build enthält das fertige Mod-Paket mindestens:

```text
coi-data-exporter/
├── manifest.json
├── CoiDataExporter.Mod.dll
├── Newtonsoft.Json.dll
└── readme.txt
```

Die Captain-of-Industry-DLLs werden **nicht** mitgeliefert. Sie sind bereits Teil der Spieleinstallation und werden nur beim Kompilieren über `COI_ROOT` referenziert.

## Voraussetzungen für die Entwicklung

Diese Voraussetzungen gelten nur, wenn du die Mod selbst kompilieren möchtest:

- Windows
- Steam-Version von Captain of Industry
- Visual Studio 2022 mit `.NET Desktop Development`
- .NET Framework 4.8 Developer Pack
- .NET SDK für das optionale CLI

Captain of Industry muss installiert sein. Setze die Umgebungsvariable `COI_ROOT` auf den Spielordner, beispielsweise:

```powershell
$coi = "C:\Program Files (x86)\Steam\steamapps\common\Captain of Industry"
[Environment]::SetEnvironmentVariable("COI_ROOT", $coi, "User")
```

Danach Visual Studio neu starten. Prüfe die DLL:

```powershell
Test-Path "$env:COI_ROOT\Captain of Industry_Data\Managed\Mafi.Core.dll"
```

## Bauen und ZIP erzeugen

Visual Studio bevorzugt verwenden:

```powershell
dotnet build .\CoiDataExporter.sln -c Release
```

Oder:

```powershell
.\tools\build.ps1
```

Der Build kopiert die Mod automatisch nach:

```text
%APPDATA%\Captain of Industry\Mods\coi-data-exporter\
```

Das ZIP-Paket ist dann ebenfalls dort verfügbar, sobald die Packaging-Option im Build aktiviert ist.

Für einen Build ohne automatisches Kopieren:

```powershell
.\tools\build.ps1 -SkipDeploy
```

## Installation des fertigen Pakets

Das fertige Paket wird nach:

```text
%APPDATA%\Captain of Industry\Mods\
```

entpackt. Danach muss die Struktur so aussehen:

```text
%APPDATA%\Captain of Industry\Mods\coi-data-exporter\
├── manifest.json
├── CoiDataExporter.Mod.dll
├── Newtonsoft.Json.dll
└── readme.txt
```

Wichtig: Die DLL darf nicht direkt lose in `Mods` liegen. Sie muss im Unterordner `coi-data-exporter` liegen.

## Dateien nach dem Start

Nach dem Laden des Spiels oder eines Spielstands:

```text
%APPDATA%\Captain of Industry\Mods\coi-data-exporter\export\coi-data.json
```

Die vollständige Datei enthält:

```json
{
  "schema_version": 1,
  "game_version": "...",
  "exported_at_utc": "...",
  "products": [],
  "recipes": [],
  "buildings": []
}
```

Zusätzlich werden für eine einfache Nutzung geschrieben:

```text
products.json
recipes.json
buildings.json
export-status.json
```

Bei einem Fehler:

```text
export-error.txt
```

Die Dateien befinden sich jeweils im Unterordner `export` des Mod-Verzeichnisses.

## Optionales Entwickler-CLI

Das CLI ist **nicht Bestandteil der notwendigen Spielinstallation**. Es dient nur dazu, einen Export am PC zu prüfen oder CSV-Dateien zu erzeugen:

```powershell
dotnet run --project .\src\CoiDataExporter.Cli -- validate `
  "$env:APPDATA\Captain of Industry\Mods\coi-data-exporter\export\coi-data.json"

dotnet run --project .\src\CoiDataExporter.Cli -- summary `
  "$env:APPDATA\Captain of Industry\Mods\coi-data-exporter\export\coi-data.json"

dotnet run --project .\src\CoiDataExporter.Cli -- csv `
  "$env:APPDATA\Captain of Industry\Mods\coi-data-exporter\export\coi-data.json" `
  ".\out\csv"
```

## Architekturentscheidungen

### Keine direkten Spielobjekte serialisieren

Die Mod mappt interne Mafi-/Unity-Objekte auf eigene DTOs. Dadurch bleibt das JSON-Schema klein, nachvollziehbar und unabhängig von internen Referenzen.

### Defensive Reflection-Grenze

Captain of Industry ist modding-seitig experimentell und interne Typen können sich ändern. Die Reflection-Anpassung ist deshalb auf `ReflectionReader.cs` und `ExportPipeline.cs` begrenzt. Das externe CLI und das Datenmodell bleiben davon getrennt.

### Versionierung

Jede Exportdatei enthält:

- `schema_version` – Version unseres JSON-Vertrages
- `game_version` – Version der geladenen COI-Basisassembly
- `exported_at_utc` – Zeitpunkt der Erzeugung

### Atomisches Schreiben

Die Mod schreibt zuerst temporäre Dateien und verschiebt sie anschließend auf die endgültigen Dateinamen. Dadurch liest kein externes Programm eine halbgeschriebene Datei.

## Bekannte Einschränkungen

- Die konkreten Prototype-Typen und Felder sind Teil der jeweiligen Spielversion.
- Die Mod sollte nach einem Spielupdate neu kompiliert und getestet werden.
- Die erste Version exportiert noch keine Icon-Assets.
- Bei einer Änderung der COI-Mod-API muss primär `CoiDataExporter.Mod` angepasst werden.

## Lizenz und Referenz

Dieses Projekt ist eine eigenständige Neuimplementierung mit einer ähnlichen Zielsetzung wie `mingl0280/coi-data-export`. Das verlinkte Projekt steht unter MIT-Lizenz und wurde als technische Referenz analysiert:

<https://github.com/mingl0280/coi-data-export>

Für die Verwendung direkt übernommener Bestandteile müssen die dortigen Copyright- und Lizenzhinweise erhalten bleiben.
