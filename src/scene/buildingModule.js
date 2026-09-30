import * as THREE from "three";
import { Palette } from "../config/palette.js";

/** Shared Bloomville block + roof prism for the web district slice. */
export function createDistrictBuilding(spec) {
  const {
    width = 2.4,
    depth = 2.2,
    floorHeight = 1.1,
    floors = 2,
    roofKind = "gable",
    roofRise = 0.85,
    wallColor = Palette.structureWall,
    roofColor = Palette.structureRoof,
  } = spec;

  const group = new THREE.Group();
  const bodyHeight = floorHeight * floors;

  const wallMat = new THREE.MeshStandardMaterial({
    color: wallColor,
    roughness: 0.78,
    metalness: 0.04,
  });
  const roofMat = new THREE.MeshStandardMaterial({
    color: roofColor,
    roughness: 0.62,
    metalness: 0.08,
  });
  const accentMat = new THREE.MeshStandardMaterial({
    color: Palette.labelCoral,
    roughness: 0.45,
    metalness: 0.12,
    emissive: Palette.labelCoral,
    emissiveIntensity: 0,
  });

  const body = new THREE.Mesh(new THREE.BoxGeometry(width, bodyHeight, depth), wallMat);
  body.position.y = bodyHeight / 2;
  body.castShadow = true;
  body.receiveShadow = true;
  group.add(body);

  const roof = buildRoof(roofKind, width, depth, roofRise, roofMat);
  roof.position.y = bodyHeight;
  roof.traverse((child) => {
    if (child.isMesh) {
      child.castShadow = true;
      child.receiveShadow = true;
    }
  });
  group.add(roof);

  const band = new THREE.Mesh(
    new THREE.BoxGeometry(width * 0.92, 0.12, depth * 0.92),
    accentMat,
  );
  band.position.y = bodyHeight * 0.55;
  band.castShadow = true;
  group.add(band);

  function setActive(active) {
    const emissive = active ? 0.22 : 0;
    wallMat.emissive.setHex(active ? Palette.labelCoral : 0x000000);
    wallMat.emissiveIntensity = emissive;
    accentMat.emissiveIntensity = active ? 0.55 : 0;
    if (active) {
      wallMat.color.setHex(Palette.surfaceRaised);
    } else {
      wallMat.color.setHex(wallColor);
    }
  }

  return { group, setActive };
}

function buildRoof(kind, width, depth, rise, material) {
  const roofGroup = new THREE.Group();

  if (kind === "flat") {
    const slab = new THREE.Mesh(new THREE.BoxGeometry(width * 1.04, 0.18, depth * 1.04), material);
    slab.position.y = 0.09;
    roofGroup.add(slab);
    return roofGroup;
  }

  if (kind === "sawtooth") {
    const step = new THREE.Mesh(new THREE.BoxGeometry(width * 0.48, rise, depth * 1.02), material);
    step.position.set(-width * 0.22, rise / 2, 0);
    roofGroup.add(step);
    const step2 = new THREE.Mesh(new THREE.BoxGeometry(width * 0.38, rise * 0.65, depth * 1.02), material);
    step2.position.set(width * 0.26, rise * 0.325, 0);
    roofGroup.add(step2);
    return roofGroup;
  }

  const geometry = new THREE.ConeGeometry(Math.max(width, depth) * 0.62, rise, 4, 1);
  const mesh = new THREE.Mesh(geometry, material);
  mesh.rotation.y = Math.PI / 4;
  mesh.position.y = rise / 2;
  roofGroup.add(mesh);
  return roofGroup;
}
