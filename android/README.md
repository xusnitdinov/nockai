# NockAI Android

Kotlin + Jetpack Compose companion app. Same Groq math brain as the Windows overlay - normal full-screen chat UI (not a system overlay).

## Download APK

Grab the prebuilt APK from GitHub Releases (no Android Studio needed):

- **[nockai-android-debug.apk](https://github.com/xusnitdinov/nockai/releases/download/v0.2.0/nockai-android-debug.apk)**
- All releases: https://github.com/xusnitdinov/nockai/releases

Install on your phone (allow unknown sources). Paste your Groq API key on first launch.

## Build from source

Needs Android Studio / JDK 17+ (Android Studio JBR works) and Android SDK 35.

```powershell
cd android
$env:JAVA_HOME = "C:\Program Files\Android\Android Studio\jbr"
.\gradlew.bat assembleDebug
```

APK output:

`android/app/build/outputs/apk/debug/app-debug.apk`

```powershell
adb install -r app\build\outputs\apk\debug\app-debug.apk
```

## First launch

Paste your Groq API key (same as Windows). Settings gear → AI tone, profile prompt, history, temp, tokens.

## Bottom bubble (Gemini-style)

On the home screen tap **Show bottom bubble**. Android will ask for “Display over other apps”. Allow it.

A clean pill docks at the bottom of the screen over any app. Tap → sheet opens → ask math → collapse with the handle. X stops the bubble (or use the notification Stop action).

## Notes

- Windows WPF app is unchanged
- Bubble needs overlay permission (sideload-friendly; Play Store later is stricter)
