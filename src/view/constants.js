/** Meters on X and Z share the same scale (isotropic ground plane). */

/** Default orthographic half-height: ~140 m vertical span fits a ~120 m street slice. */
export const DEFAULT_HALF_HEIGHT_M = 70;

/** Fixed elevation angle (radians); no free orbit in this department. */
export const FIXED_PITCH_RAD = (52 * Math.PI) / 180;

/** Keep the look target inside the playable district, not the open world. */
export const TARGET_RADIUS_M = 200;

/** Prevent zooming into a single façade (~20 m lots). */
export const MIN_HALF_HEIGHT_M = 22;

/** Prevent pulling back to continent / planet scale. */
export const MAX_HALF_HEIGHT_M = 130;
