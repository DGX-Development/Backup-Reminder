// Render the unchanged vector master with supersampling; no artwork geometry is edited.
// Requires Node.js and sharp (SHARP_MODULE may point to an existing sharp installation).
const fs = require('node:fs');
const path = require('node:path');
const sharp = require(process.env.SHARP_MODULE || 'sharp');
const root = path.resolve(__dirname, '..');
const svg = fs.readFileSync(path.join(root, 'assets', 'backup-reminder.svg'));
async function render(size) {
  const large = await sharp(svg, {density: 576}).resize(size * 4, size * 4).png().toBuffer();
  return sharp(large).resize(size, size, {kernel:'lanczos3'}).png().toBuffer();
}
(async () => {
  fs.writeFileSync(path.join(root,'assets','backup-reminder.png'), await render(1024));
  const sizes=[16,20,24,32,40,48,64,128,256];
  const frames=await Promise.all(sizes.map(render));
  const header=Buffer.alloc(6+16*sizes.length);header.writeUInt16LE(1,2);header.writeUInt16LE(sizes.length,4);
  let offset=header.length;
  frames.forEach((frame,i)=>{const at=6+16*i,size=sizes[i];header[at]=size===256?0:size;header[at+1]=size===256?0:size;header.writeUInt16LE(1,at+4);header.writeUInt16LE(32,at+6);header.writeUInt32LE(frame.length,at+8);header.writeUInt32LE(offset,at+12);offset+=frame.length;});
  fs.writeFileSync(path.join(root,'assets','backup-reminder.ico'),Buffer.concat([header,...frames]));
  console.log('Rendered PNG and ICO from the unchanged SVG.');
})().catch(error=>{console.error(error);process.exitCode=1;});
