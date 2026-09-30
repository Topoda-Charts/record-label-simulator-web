import {
  DEFAULT_HALF_HEIGHT_M,
  DEFAULT_TARGET_X_M,
  DEFAULT_TARGET_Z_M,
  FIXED_PITCH_RAD,
  MAX_HALF_HEIGHT_M,
  MIN_HALF_HEIGHT_M,
  TARGET_RADIUS_M,
} from "./constants.js";

/**
 * @typedef {{ x: number, z: number }} GroundTarget
 * @typedef {{ target: GroundTarget, halfHeight: number, pitch: number }} GodViewState
 */

/** @returns {GodViewState} */
export function createDefaultViewState() {
  return {
    target: { x: DEFAULT_TARGET_X_M, z: DEFAULT_TARGET_Z_M },
    halfHeight: DEFAULT_HALF_HEIGHT_M,
    pitch: FIXED_PITCH_RAD,
  };
}

/**
 * @param {GroundTarget} target
 * @returns {GroundTarget}
 */
export function clampTarget(target) {
  const { x, z } = target;
  const radius = Math.hypot(x, z);
  if (radius <= TARGET_RADIUS_M || radius === 0) {
    return { x, z };
  }
  const scale = TARGET_RADIUS_M / radius;
  return { x: x * scale, z: z * scale };
}

/**
 * @param {number} halfHeight
 * @returns {number}
 */
export function clampHalfHeight(halfHeight) {
  if (halfHeight < MIN_HALF_HEIGHT_M) {
    return MIN_HALF_HEIGHT_M;
  }
  if (halfHeight > MAX_HALF_HEIGHT_M) {
    return MAX_HALF_HEIGHT_M;
  }
  return halfHeight;
}

/**
 * Pan the look target on the ground plane (meters).
 * Positive dx moves +X; positive dy moves +Z.
 *
 * @param {GodViewState} state
 * @param {number} dx
 * @param {number} dy
 * @returns {GodViewState}
 */
export function pan(state, dx, dy) {
  const target = clampTarget({
    x: state.target.x + dx,
    z: state.target.z + dy,
  });
  return { ...state, target };
}

/**
 * Scale orthographic half-height by factor (>1 zooms out, <1 zooms in), then clamp.
 *
 * @param {GodViewState} state
 * @param {number} factor
 * @returns {GodViewState}
 */
export function zoom(state, factor) {
  if (!Number.isFinite(factor) || factor <= 0) {
    return state;
  }
  const halfHeight = clampHalfHeight(state.halfHeight * factor);
  return { ...state, halfHeight };
}
