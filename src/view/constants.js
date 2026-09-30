/** Meters on X and Z share the same scale (isotropic ground plane). */

/** Default orthographic half-height: frames ~100 m × 70 m ground slice at 16:10. */
export const DEFAULT_HALF_HEIGHT_M = 28;

/** Default ground look target (m): between Member and City Hall, not open sky. */
export const DEFAULT_TARGET_X_M = 0;
export const DEFAULT_TARGET_Z_M = -24;

/** Fixed elevation angle (radians); no free orbit in this department. */
export const FIXED_PITCH_RAD = (52 * Math.PI) / 180;

/** Keep the look target inside the playable district, not the open world. */
export const TARGET_RADIUS_M = 200;

/** Prevent zooming into a single façade (~20 m lots). */
export const MIN_HALF_HEIGHT_M = 22;

/** Prevent pulling back to continent / planet scale. */
export const MAX_HALF_HEIGHT_M = 130;
