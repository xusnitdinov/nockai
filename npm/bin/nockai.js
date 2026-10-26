#!/usr/bin/env node
"use strict";

const { main } = require("../lib/cli");

main(process.argv.slice(2)).catch((err) => {
  console.error(`\nNockAI error: ${err.message || err}`);
  process.exit(1);
});
