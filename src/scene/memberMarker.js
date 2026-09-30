import * as THREE from "three";
import { Palette } from "../config/palette.js";

const MEMBER_HEIGHT_M = 1.8;

/** One Community Member at human scale (~1.8 m), feet at group origin. */
export function createMemberMarker() {
  const group = new THREE.Group();

  const bodyMat = new THREE.MeshStandardMaterial({
    color: Palette.ink,
    roughness: 0.82,
    metalness: 0.04,
  });
  const skinMat = new THREE.MeshStandardMaterial({
    color: 0xc9a88a,
    roughness: 0.78,
    metalness: 0.02,
  });

  const legH = 0.82;
  const torsoH = 0.72;
  const headR = 0.22;

  const legs = new THREE.Mesh(new THREE.CylinderGeometry(0.18, 0.2, legH, 10), bodyMat);
  legs.position.y = legH / 2;
  legs.castShadow = true;

  const torso = new THREE.Mesh(new THREE.CylinderGeometry(0.28, 0.32, torsoH, 12), bodyMat);
  torso.position.y = legH + torsoH / 2;
  torso.castShadow = true;

  const head = new THREE.Mesh(new THREE.SphereGeometry(headR, 14, 12), skinMat);
  head.position.y = legH + torsoH + headR * 0.95;
  head.castShadow = true;

  group.add(legs, torso, head);

  const total = legH + torsoH + headR * 2 * 0.95;
  const scale = MEMBER_HEIGHT_M / total;
  group.scale.setScalar(scale);

  return { group, heightM: MEMBER_HEIGHT_M };
}
