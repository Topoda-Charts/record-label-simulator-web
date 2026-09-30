/**
 * Nation and UI colors aligned with ObserverPalette in the Windows HDRP repo
 * (Annglora #CC99FF, Byteria #3333FF, Crownia #FFD700; app surfaces #FAF7F2).
 * Annglora flora green accent (#8FB996 semantic success) follows Gaia observer brief.
 */
export const Palette = {
  appBg: 0xfaf7f2,
  surfaceCard: 0xf2ede6,
  surfaceRaised: 0xe8e2da,
  ink: 0x3f3a36,
  labelCoral: 0xe88b7d,
  annglora: 0xcc99ff,
  anngloraFlora: 0x8fb996,
  byteria: 0x3333ff,
  crownia: 0xffd700,
  groundTint: 0x5f6d58,
  plaza: 0xd4c8b8,
  stone: 0xe4d9cc,
  road: 0x6e675f,
  sidewalk: 0xcfc4b6,
  structureWall: 0xd7c6c0,
  structureWallLilac: 0xcbb8d4,
  structureRoof: 0x6d587f,
  structureRoofWarm: 0x8d6a62,
  ground: 0x2a2630,
};

export function hexColor(hex) {
  return `#${hex.toString(16).padStart(6, "0")}`;
}
