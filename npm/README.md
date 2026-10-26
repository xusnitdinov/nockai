# nockai (npm installer)

Install [NockAI](https://github.com/xusnitdinov/nockai) on Windows with one command:

```bash
npx nockai
```

## Commands

```bash
npx nockai              # install/update + launch
npx nockai install      # install/update only
npx nockai start        # launch
npx nockai uninstall    # remove app (keeps API key)
npx nockai doctor       # sanity check
```

Prefers the latest GitHub release zip (`NockAI-win-x64.zip`). If none exists yet, builds from source (needs .NET 9 SDK + git).

First launch asks for a free [Groq API key](https://console.groq.com/keys).

## Requirements

- Windows 10/11
- Node 18+ (for `npx` only)
- For source fallback: [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
