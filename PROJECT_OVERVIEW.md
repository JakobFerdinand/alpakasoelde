# Projektbeschreibung

Dieses Repository enthält den Quellcode für die Webseite **Alpakasölde**. Die Seite dient als Online-Präsenz einer Alpaka-Farm und wurde mit dem statischen Site-Generator **Astro** erstellt. Der Fokus liegt auf einer übersichtlichen Struktur, damit sie später von LLM Agents leicht erweitert oder angepasst werden kann.

## Verwendete Technologien

- **Astro**: Framework für statische Webseiten (siehe `package.json`).
- **Node.js**: Laufzeitumgebung für die Entwicklung und den Build-Prozess.
- **CSS**: Styling der Komponenten in `src/styles/global.css`.
- **Astro Components**: Seiten und Komponenten befinden sich unter `src/` und nutzen die `.astro` Syntax.
- **TypeScript-Konfiguration**: `tsconfig.json` enthält eine strikte Voreinstellung für Astro.
- **Azure Static Web Apps**: Hosting und Deployment erfolgen über GitHub Actions (`.github/workflows/ci.yml`). Die Laufzeitkonfiguration steht in `staticwebapp.config.json`.
- **Azure Functions**: Unter `Api/` liegt eine isolierte .NET 9 Function (`SendContactFunction`), die POST-Anfragen annimmt.
- **Azure Table Storage**: Die Bereitstellung des Speichers kann mit dem Bicep-Skript `infrastructure/table-storage.bicep` erfolgen.

## Azure Funktionsweise

- Der CI-Workflow `.github/workflows/ci.yml` baut sowohl die Astro-Seite als auch das API-Projekt und deployt beides als Azure Static Web App.
- Die API ist unter `/api` erreichbar; `SendContactFunction` nimmt dort POST-Daten entgegen und bestätigt mit "ok".
- Mit `infrastructure/table-storage.bicep` kann bei Bedarf eine Azure Table zur Speicherung solcher Daten erstellt werden.

## Hinweise für LLM Agents

- Der zentrale Einstiegspunkt für die Anwendung ist `src/pages/index.astro`.
- Die globalen Styles werden in `src/styles/global.css` definiert.
- Entwicklungs- und Build-Skripte sind in `package.json` beschrieben (`npm run dev`, `npm run build`).
- Farbdefinitionen, die für das Styling relevant sind, finden sich sowohl in `README.md` als auch in der CSS-Datei.

Dieses Dokument soll künftige Automatisierungen durch LLM Agents unterstützen, indem es eine kompakte Übersicht über das Projekt liefert.
