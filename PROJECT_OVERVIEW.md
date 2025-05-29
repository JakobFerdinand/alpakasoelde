# Projektbeschreibung

Dieses Repository enthält den Quellcode für die Webseite **Alpakasölde**. Die Seite dient als Online-Präsenz einer Alpaka-Farm und wurde mit dem statischen Site-Generator **Astro** erstellt. Der Fokus liegt auf einer übersichtlichen Struktur, damit sie später von LLM Agents leicht erweitert oder angepasst werden kann.

## Verwendete Technologien

- **Astro**: Framework für statische Webseiten (siehe `package.json`).
- **Node.js**: Laufzeitumgebung für die Entwicklung und den Build-Prozess.
- **CSS**: Styling der Komponenten in `src/styles/global.css`.
- **Astro Components**: Seiten und Komponenten befinden sich unter `src/` und nutzen die `.astro` Syntax.
- **TypeScript-Konfiguration**: `tsconfig.json` enthält eine strikte Voreinstellung für Astro.

## Hinweise für LLM Agents

- Der zentrale Einstiegspunkt für die Anwendung ist `src/pages/index.astro`.
- Die globalen Styles werden in `src/styles/global.css` definiert.
- Entwicklungs- und Build-Skripte sind in `package.json` beschrieben (`npm run dev`, `npm run build`).
- Farbdefinitionen, die für das Styling relevant sind, finden sich sowohl in `README.md` als auch in der CSS-Datei.

Dieses Dokument soll künftige Automatisierungen durch LLM Agents unterstützen, indem es eine kompakte Übersicht über das Projekt liefert.
