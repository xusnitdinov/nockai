"use strict";

const fs = require("fs");
const os = require("os");
const path = require("path");
const { spawnSync, spawn } = require("child_process");
const https = require("https");
const http = require("http");
const { pipeline } = require("stream/promises");
const { createWriteStream } = require("fs");

const REPO = "xusnitdinov/nockai";
const ASSET_NAME = "NockAI-win-x64.zip";

function installDir() {
  return path.join(process.env.LOCALAPPDATA || path.join(os.homedir(), "AppData", "Local"), "NockAI");
}

function exePath() {
  return path.join(installDir(), "NockAI.exe");
}

function startupLnk() {
  return path.join(
    process.env.APPDATA || path.join(os.homedir(), "AppData", "Roaming"),
    "Microsoft",
    "Windows",
    "Start Menu",
    "Programs",
    "Startup",
    "NockAI.lnk"
  );
}

function assertWindows() {
  if (process.platform !== "win32") {
    throw new Error("NockAI is Windows-only (Win10/11).");
  }
}

function printHelp() {
  console.log(`
NockAI - Ctrl+Space calculator for Windows

Usage:
  npx nockai              Install (or update) and launch
  npx nockai install      Install / update only
  npx nockai start        Launch installed app
  npx nockai uninstall    Remove app + startup shortcut
  npx nockai doctor       Check install / .NET

Needs a free Groq key on first launch: https://console.groq.com/keys
`);
}

function run(cmd, args, opts = {}) {
  const r = spawnSync(cmd, args, {
    stdio: "inherit",
    shell: false,
    ...opts,
  });
  if (r.error) throw r.error;
  if (r.status !== 0) throw new Error(`${cmd} exited with ${r.status}`);
}

function which(cmd) {
  const r = spawnSync(cmd, ["--version"], { encoding: "utf8", shell: true });
  return r.status === 0;
}

function httpGetJson(url) {
  return new Promise((resolve, reject) => {
    const lib = url.startsWith("https") ? https : http;
    lib
      .get(
        url,
        {
          headers: {
            "User-Agent": "nockai-npm-installer",
            Accept: "application/vnd.github+json",
          },
        },
        (res) => {
          if (res.statusCode >= 300 && res.statusCode < 400 && res.headers.location) {
            httpGetJson(res.headers.location).then(resolve, reject);
            return;
          }
          let data = "";
          res.on("data", (c) => (data += c));
          res.on("end", () => {
            if (res.statusCode !== 200) {
              reject(new Error(`GitHub API ${res.statusCode}: ${data.slice(0, 200)}`));
              return;
            }
            try {
              resolve(JSON.parse(data));
            } catch (e) {
              reject(e);
            }
          });
        }
      )
      .on("error", reject);
  });
}

function downloadFile(url, dest) {
  return new Promise((resolve, reject) => {
    const lib = url.startsWith("https") ? https : http;
    lib
      .get(
        url,
        { headers: { "User-Agent": "nockai-npm-installer", Accept: "application/octet-stream" } },
        (res) => {
          if (res.statusCode >= 300 && res.statusCode < 400 && res.headers.location) {
            downloadFile(res.headers.location, dest).then(resolve, reject);
            return;
          }
          if (res.statusCode !== 200) {
            reject(new Error(`Download failed HTTP ${res.statusCode}`));
            return;
          }
          const out = createWriteStream(dest);
          pipeline(res, out).then(resolve).catch(reject);
        }
      )
      .on("error", reject);
  });
}

async function latestReleaseAsset() {
  const release = await httpGetJson(`https://api.github.com/repos/${REPO}/releases/latest`);
  const asset = (release.assets || []).find((a) => a.name === ASSET_NAME);
  if (!asset) {
    throw new Error(`No ${ASSET_NAME} on latest release (${release.tag_name || "?"})`);
  }
  return { tag: release.tag_name, url: asset.browser_download_url };
}

function expandZip(zipPath, destDir) {
  fs.mkdirSync(destDir, { recursive: true });
  // PowerShell Expand-Archive is reliable on Win10+
  run("powershell.exe", [
    "-NoProfile",
    "-ExecutionPolicy",
    "Bypass",
    "-Command",
    `Expand-Archive -LiteralPath '${zipPath.replace(/'/g, "''")}' -DestinationPath '${destDir.replace(/'/g, "''")}' -Force`,
  ]);
}

function createStartupShortcut() {
  const exe = exePath();
  const lnk = startupLnk();
  const dir = installDir();
  const ps = `
$w = New-Object -ComObject WScript.Shell
$lnk = $w.CreateShortcut('${lnk.replace(/'/g, "''")}')
$lnk.TargetPath = '${exe.replace(/'/g, "''")}'
$lnk.WorkingDirectory = '${dir.replace(/'/g, "''")}'
$lnk.Description = 'NockAI - Ctrl+Space calculator'
$lnk.Save()
`;
  run("powershell.exe", ["-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", ps]);
}

function launch() {
  const exe = exePath();
  if (!fs.existsSync(exe)) throw new Error(`Not installed. Run: npx nockai install`);
  spawn(exe, [], { detached: true, stdio: "ignore" }).unref();
  console.log("Launched NockAI. Hotkey: Ctrl+Space");
}

async function installFromRelease() {
  console.log("Fetching latest NockAI release...");
  const { tag, url } = await latestReleaseAsset();
  console.log(`Downloading ${tag} (${ASSET_NAME})...`);

  const tmp = fs.mkdtempSync(path.join(os.tmpdir(), "nockai-"));
  const zip = path.join(tmp, ASSET_NAME);
  try {
    await downloadFile(url, zip);
    const dest = installDir();
    // clear old binaries but keep secrets
    if (fs.existsSync(dest)) {
      for (const name of fs.readdirSync(dest)) {
        if (name.toLowerCase().includes("secrets")) continue;
        const p = path.join(dest, name);
        fs.rmSync(p, { recursive: true, force: true });
      }
    } else {
      fs.mkdirSync(dest, { recursive: true });
    }

    const extractTo = path.join(tmp, "out");
    expandZip(zip, extractTo);

    // zip may contain files at root or in a folder
    let src = extractTo;
    const kids = fs.readdirSync(extractTo);
    if (kids.length === 1 && fs.statSync(path.join(extractTo, kids[0])).isDirectory()) {
      src = path.join(extractTo, kids[0]);
    }

    for (const name of fs.readdirSync(src)) {
      fs.cpSync(path.join(src, name), path.join(dest, name), { recursive: true });
    }

    if (!fs.existsSync(exePath())) {
      throw new Error("Install finished but NockAI.exe missing - bad zip layout");
    }

    createStartupShortcut();
    console.log(`Installed ${tag} → ${dest}`);
    console.log("Startup shortcut added. First launch may ask for a Groq API key.");
  } finally {
    fs.rmSync(tmp, { recursive: true, force: true });
  }
}

async function installFromSource() {
  console.log("No release zip found - building from source...");
  if (!which("dotnet")) {
    throw new Error(
      "Need .NET 9 SDK to build from source. Install: winget install Microsoft.DotNet.SDK.9\nOr wait for a GitHub release zip."
    );
  }
  if (!which("git")) {
    throw new Error("git not found. Install Git for Windows, or use a release build.");
  }

  const tmp = fs.mkdtempSync(path.join(os.tmpdir(), "nockai-src-"));
  try {
    run("git", ["clone", "--depth", "1", `https://github.com/${REPO}.git`, tmp]);
    const proj = path.join(tmp, "src", "NockAI", "NockAI.csproj");
    const dest = installDir();
    fs.mkdirSync(dest, { recursive: true });
    console.log("Publishing Release (self-contained)...");
    run("dotnet", [
      "publish",
      proj,
      "-c",
      "Release",
      "-r",
      "win-x64",
      "--self-contained",
      "true",
      "-o",
      dest,
      "/p:PublishSingleFile=true",
      "/p:IncludeNativeLibrariesForSelfExtract=true",
    ]);
    createStartupShortcut();
    console.log(`Built & installed → ${dest}`);
  } finally {
    fs.rmSync(tmp, { recursive: true, force: true });
  }
}

async function install() {
  assertWindows();
  try {
    await installFromRelease();
  } catch (e) {
    console.warn(`Release install skipped: ${e.message}`);
    await installFromSource();
  }
}

function uninstall() {
  assertWindows();
  const dest = installDir();
  const lnk = startupLnk();
  spawnSync("taskkill", ["/IM", "NockAI.exe", "/F"], { stdio: "ignore", shell: true });
  if (fs.existsSync(lnk)) {
    fs.unlinkSync(lnk);
    console.log("Removed startup shortcut");
  }
  if (fs.existsSync(dest)) {
    // keep secrets unless user wants wipe - ask via env
    if (process.env.NOCKAI_WIPE_SECRETS === "1") {
      fs.rmSync(dest, { recursive: true, force: true });
      console.log(`Removed ${dest}`);
    } else {
      for (const name of fs.readdirSync(dest)) {
        if (name.toLowerCase().includes("secrets")) continue;
        fs.rmSync(path.join(dest, name), { recursive: true, force: true });
      }
      console.log(`Removed app files in ${dest} (secrets kept)`);
      console.log("Set NOCKAI_WIPE_SECRETS=1 to delete the API key too.");
    }
  } else {
    console.log("Nothing installed.");
  }
}

function doctor() {
  assertWindows();
  console.log(`Platform: ${process.platform} ${os.release()}`);
  console.log(`Install dir: ${installDir()}`);
  console.log(`Exe: ${fs.existsSync(exePath()) ? "yes" : "missing"} → ${exePath()}`);
  console.log(`Startup lnk: ${fs.existsSync(startupLnk()) ? "yes" : "no"}`);
  console.log(`dotnet: ${which("dotnet") ? "yes" : "no"}`);
  console.log(`git: ${which("git") ? "yes" : "no"}`);
}

async function main(argv) {
  const cmd = (argv[0] || "install-and-start").toLowerCase();

  if (cmd === "-h" || cmd === "--help" || cmd === "help") {
    printHelp();
    return;
  }

  if (cmd === "doctor") {
    doctor();
    return;
  }

  if (cmd === "uninstall" || cmd === "remove") {
    uninstall();
    return;
  }

  if (cmd === "start" || cmd === "run" || cmd === "open") {
    assertWindows();
    launch();
    return;
  }

  if (cmd === "install" || cmd === "i" || cmd === "update") {
    await install();
    return;
  }

  // default: install (or update) then start
  if (cmd === "install-and-start") {
    await install();
    launch();
    return;
  }

  printHelp();
  throw new Error(`Unknown command: ${cmd}`);
}

module.exports = { main };
