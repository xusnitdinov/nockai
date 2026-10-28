# NockAI Android

Kotlin + Jetpack Compose companion app. Same Groq math brain as the Windows overlay - normal full-screen chat UI (not a system overlay).

## Requirements

- Android Studio / JDK 17+ (Android Studio JBR works)
- Android SDK 35
- Phone or emulator, API 26+

## Build APK

```powershell
cd android
$env:JAVA_HOME = "C:\Program Files\Android\Android Studio\jbr"
.\gradlew.bat assembleDebug
```

APK path:

`android/app/build/outputs/apk/debug/app-debug.apk`

Install:

```powershell
adb install -r app\build\outputs\apk\debug\app-debug.apk
```

Or copy the APK to your phone and open it (allow install from unknown sources).

## First launch

Paste your Groq API key (same as Windows). Settings gear → AI tone, profile prompt, history, temp, tokens.

## Notes

- Windows WPF app is unchanged
- Floating bubble / Accessibility overlay is not in v1
