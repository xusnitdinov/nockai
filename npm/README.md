# @xusnitdinov/nockai

Install [NockAI](https://github.com/xusnitdinov/nockai) on Windows with one command:

```bash
npx @xusnitdinov/nockai
```

## Commands

```bash
npx @xusnitdinov/nockai              # install/update + launch
npx @xusnitdinov/nockai install      # install/update only
npx @xusnitdinov/nockai start        # launch
npx @xusnitdinov/nockai uninstall    # remove app (keeps API key)
npx @xusnitdinov/nockai doctor       # sanity check
```

Prefers the latest GitHub release zip (`NockAI-win-x64.zip`). If none exists yet, builds from source (needs .NET 9 SDK + git).

First launch asks for a free [Groq API key](https://console.groq.com/keys).

## Requirements

- Windows 10/11
- Node 18+ (for `npx` only)
- For source fallback: [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
