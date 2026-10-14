import { defineConfig, minimal2023Preset } from '@vite-pwa/assets-generator/config'

const background = '#2a3a20'

// Pixel art: nearest-neighbour scaling keeps the favicon pixels sharp at 512 px.
const pixelArt = { kernel: 'nearest' } as const

export default defineConfig({
  headLinkOptions: { preset: '2023' },
  preset: {
    ...minimal2023Preset,
    transparent: { ...minimal2023Preset.transparent, resizeOptions: pixelArt },
    // Android may crop maskable icons to a circle, so the square castle needs extra room to keep its corners.
    maskable: { ...minimal2023Preset.maskable, padding: 0.42, resizeOptions: { ...pixelArt, background } },
    apple: { ...minimal2023Preset.apple, resizeOptions: { ...pixelArt, background } },
  },
  images: ['public/favicon.svg'],
})
