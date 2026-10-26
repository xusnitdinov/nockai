# NockAI

Win11 hotkey calculator. **Ctrl+Space**, type math or English, get an answer. Tray app, low RAM.

AI turns natural language into an expression. **mXparser** does the actual math - so the model doesn't invent the number.

![.NET 9](https://img.shields.io/badge/.NET-9-512BD4) ![Windows](https://img.shields.io/badge/Windows-11%20%2F%2010-0078D4) ![License](https://img.shields.io/badge/license-MIT-green)

## Install (Windows)

**Easiest (Node / npx):**

```bash
npx github:xusnitdinov/nockai
```

Or after we publish to npm: `npx nockai`

That downloads the latest Windows build into `%LOCALAPPDATA%\NockAI`, adds a Startup shortcut, and launches. First run asks for a free [Groq API key](https://console.groq.com/keys).

```bash
npx github:xusnitdinov/nockai install
npx github:xusnitdinov/nockai start
npx github:xusnitdinov/nockai uninstall
npx github:xusnitdinov/nockai doctor
```

**From source (PowerShell):** needs [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0).

```powershell
git clone https://github.com/xusnitdinov/nockai.git
cd nockai
powershell -ExecutionPolicy Bypass -File .\scripts\install.ps1
```

### Dev run

```powershell
cd src\NockAI
copy Secrets.local.json.example Secrets.local.json
# edit Secrets.local.json - paste GroqApiKey
dotnet run -c Release
```

## Hotkeys

| Key | Action |
|-----|--------|
| **Ctrl+Space** | Show / hide |
| **Esc** / click away | Hide |
| **Ctrl+C** | Copy answer (when overlay is open) |
| **Enter** | Evaluate / follow-up |

Tray icon: left-click open, right-click Exit.

## What it can do

- Symbolic: `2+3^4`, `sqrt(144)`, `5!`, `nCr(10,3)`
- NL arithmetic: `multiple 264 by 1446`, percents, ratios
- Stats: mean / median / mode / range / variance, frequency means
- Algebra: `12n=144 find the n`
- Explain mode + follow-ups (`more examples pls`)
- Nice fractions: `(8-3)/8` → **5/8** (0.625)

Local patterns answer without the network when they can. Groq is only for wordy / explain stuff.

## How it works

1. Local engine first (symbols + common NL)
2. Else Groq (`qwen/qwen3.8-27b`) returns JSON: `calc` / `explain` / `unknown`
3. Expressions are evaluated with mXparser (AI never writes the final number alone)
4. Input is debounced; typing cancels the previous request

## Secrets

Never commit `Secrets.local.json` (gitignored). Prefer AppData after install.

If a key ever leaked in chat, rotate it in the [Groq console](https://console.groq.com/keys).

## Stack

- .NET 9 WPF + Win11 acrylic / rounded chrome
- Groq for NL parsing
- mXparser for evaluation

## License

MIT - see [LICENSE](LICENSE).
