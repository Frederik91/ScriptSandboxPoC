const path = require('path');
const fs = require('fs');

const distDir = path.join(__dirname, 'dist');
if (!fs.existsSync(distDir)) {
  console.error('dist folder not found. Run "npm run build" first.');
  process.exit(1);
}

const pipelineDir = path.join(__dirname, '..');
const filesToCopy = [
  path.join(pipelineDir, 'sdk', 'scriptbox.js'),
  path.join(distDir, 'demo-api.js'),
];

const targetDir = path.join(__dirname, '..', 'dist');
fs.mkdirSync(targetDir, { recursive: true });

filesToCopy.forEach(file => {
  const filename = path.basename(file);
  fs.copyFileSync(file, path.join(targetDir, filename));
  console.log('Copied', filename, '->', targetDir);
});
