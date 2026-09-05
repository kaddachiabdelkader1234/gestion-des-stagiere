const { spawnSync } = require('child_process');
const path = require('path');

async function main() {
  const puppeteer = require('puppeteer');
  process.env.CHROME_BIN = await puppeteer.executablePath();

  const ngBin = path.join(__dirname, '..', 'node_modules', '@angular', 'cli', 'bin', 'ng.js');

  const result = spawnSync(process.execPath, [ngBin, 'test', '--watch=false'], {
    stdio: 'inherit',
    env: process.env,
    shell: false
  });

  process.exit(result.status ?? 1);
}

main().catch((error) => {
  console.error(error);
  process.exit(1);
});