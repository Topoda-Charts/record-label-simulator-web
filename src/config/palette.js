/**
 * Nation and UI colors aligned with ObserverPalette in the Windows HDRP repo
 * (Annglora #CC99FF, Byteria #3333FF, Crownia #FFD700; app surfaces #FAF7F2).
 * Annglora flora green accent (#8FB996 semantic success) follows Gaia observer brief.
 */
export const Palette = {
  appBg: 0xfaf7f2,
  ink: 0x3f3a36,
  labelCoral: 0xe88b7d,
  annglora: 0xcc99ff,
  anngloraFlora: 0x8fb996,
  byteria: 0x3333ff,
  crownia: 0xffd700,
  ground: 0x2a2630,
  road: 0x4a4550,
};

export function hexColor(hex) {
  return `#${hex.toString(16).padStart(6, "0")}`;
}
