import * as THREE from "three";
import { Palette } from "../config/palette.js";

/** Shared Bloomville block: windowed facade, trim, and a real roof slope. */
export function createDistrictBuilding(spec) {
  const {
    width = 2.4,
    depth = 2.2,
    floorHeight = 1.15,
    floors = 2,
    roofKind = "gable",
    roofRise = 0.85,
    wallColor = Palette.structureWall,
    roofColor = Palette.structureRoof,
    windowGlow = 0.15,
  } = spec;

  const group = new THREE.Group();
  const bodyHeight = floorHeight * floors;
  const facade = makeFacadeTexture(wallColor, floors);

  const wallMat = new THREE.MeshStandardMaterial({
    map: facade,
    roughness: 0.82,
    metalness: 0.03,
  });
  const roofMat = new THREE.MeshStandardMaterial({
    color: roofColor,
    roughness: 0.58,
    metalness: 0.06,
  });
  const trimMat = new THREE.MeshStandardMaterial({
    color: Palette.stone,
    roughness: 0.7,
    metalness: 0.04,
  });

  const sideMat = new THREE.MeshStandardMaterial({
    color: wallColor,
    roughness: 0.86,
    metalness: 0.02,
  });
  const body = new THREE.Mesh(
    new THREE.BoxGeometry(width, bodyHeight, depth),
    [
      sideMat,
      sideMat,
      trimMat,
      trimMat,
      wallMat,
      wallMat,
    ],
  );
  body.position.y = bodyHeight / 2;
  body.castShadow = true;
  body.receiveShadow = true;
  group.add(body);

  const plinth = new THREE.Mesh(
    new THREE.BoxGeometry(width + 0.4, 0.35, depth + 0.4),
    trimMat,
  );
  plinth.position.y = 0.17;
  plinth.castShadow = true;
  plinth.receiveShadow = true;
  group.add(plinth);

  const roof = buildRoof(roofKind, width, depth, roofRise, roofMat);
  roof.position.y = bodyHeight;
  roof.traverse((child) => {
    if (child.isMesh) {
      child.castShadow = true;
      child.receiveShadow = true;
    }
  });
  group.add(roof);

  const glow = new THREE.PointLight(Palette.labelCoral, 0, 6, 2);
  glow.position.set(0, bodyHeight * 0.55, depth * 0.55);
  group.add(glow);

  function setActive(active) {
    wallMat.emissive.setHex(active ? Palette.labelCoral : 0x000000);
    wallMat.emissiveIntensity = active ? 0.18 : windowGlow * 0.02;
    glow.intensity = active ? 1.4 : windowGlow;
  }

  setActive(false);

  return { group, setActive };
}

/** Civic City Hall — larger than a lot block; base mass plus distinct roof/dome. */
export function createCityHall() {
  const width = 40;
  const depth = 28;
  const floorHeight = 4.2;
  const floors = 2;
  const bodyHeight = floorHeight * floors;
  const group = new THREE.Group();

  const wallMat = new THREE.MeshStandardMaterial({
    color: Palette.structureWallLilac,
    roughness: 0.78,
    metalness: 0.04,
  });
  const trimMat = new THREE.MeshStandardMaterial({
    color: Palette.stone,
    roughness: 0.72,
    metalness: 0.05,
  });
  const accentMat = new THREE.MeshStandardMaterial({
    color: Palette.annglora,
    roughness: 0.65,
    metalness: 0.08,
  });
  const domeMat = new THREE.MeshStandardMaterial({
    color: Palette.crownia,
    roughness: 0.42,
    metalness: 0.22,
  });

  const plinth = new THREE.Mesh(
    new THREE.BoxGeometry(width + 2.4, 1.2, depth + 2.4),
    trimMat,
  );
  plinth.position.y = 0.6;
  plinth.castShadow = true;
  plinth.receiveShadow = true;
  group.add(plinth);

  const body = new THREE.Mesh(new THREE.BoxGeometry(width, bodyHeight, depth), wallMat);
  body.position.y = 1.2 + bodyHeight / 2;
  body.castShadow = true;
  body.receiveShadow = true;
  group.add(body);

  const portico = new THREE.Mesh(new THREE.BoxGeometry(14, floorHeight * 0.85, 3.2), accentMat);
  portico.position.set(0, 1.2 + floorHeight * 0.42, depth / 2 + 1.4);
  portico.castShadow = true;
  portico.receiveShadow = true;
  group.add(portico);

  const columns = 4;
  for (let i = 0; i < columns; i += 1) {
    const col = new THREE.Mesh(new THREE.CylinderGeometry(0.55, 0.65, floorHeight * 0.75, 8), trimMat);
    const x = -4.5 + i * 3;
    col.position.set(x, 1.2 + floorHeight * 0.38, depth / 2 + 2.6);
    col.castShadow = true;
    group.add(col);
  }

  const roofSlab = new THREE.Mesh(
    new THREE.BoxGeometry(width + 1.2, 1.0, depth + 1.2),
    trimMat,
  );
  roofSlab.position.y = 1.2 + bodyHeight + 0.5;
  roofSlab.castShadow = true;
  roofSlab.receiveShadow = true;
  group.add(roofSlab);

  const drum = new THREE.Mesh(new THREE.CylinderGeometry(5.5, 5.5, 2.8, 16), accentMat);
  drum.position.y = 1.2 + bodyHeight + 1.0 + 1.4;
  drum.castShadow = true;
  group.add(drum);

  const dome = new THREE.Mesh(new THREE.SphereGeometry(6.2, 18, 12, 0, Math.PI * 2, 0, Math.PI / 2), domeMat);
  dome.position.y = 1.2 + bodyHeight + 2.8 + 2.4;
  dome.castShadow = true;
  group.add(dome);

  const spire = new THREE.Mesh(new THREE.CylinderGeometry(0.35, 0.55, 2.2, 8), trimMat);
  spire.position.y = 1.2 + bodyHeight + 2.8 + 6.2;
  spire.castShadow = true;
  group.add(spire);

  function setActive(active) {
    wallMat.emissive.setHex(active ? Palette.labelCoral : 0x000000);
    wallMat.emissiveIntensity = active ? 0.12 : 0;
  }
  setActive(false);

  return { group, setActive };
}

function buildRoof(kind, width, depth, rise, material) {
  const roofGroup = new THREE.Group();

  if (kind === "flat") {
    const slab = new THREE.Mesh(
      new THREE.BoxGeometry(width + 0.5, 0.45, depth + 0.5),
      material,
    );
    slab.position.y = 0.22;
    const lip = new THREE.Mesh(
      new THREE.BoxGeometry(width + 0.65, 0.12, depth + 0.65),
      material,
    );
    lip.position.y = 0.48;
    roofGroup.add(slab, lip);
    return roofGroup;
  }

  if (kind === "sawtooth") {
    const step = new THREE.Mesh(new THREE.BoxGeometry(width * 0.52, rise, depth + 0.2), material);
    step.position.set(-width * 0.22, rise / 2, 0);
    const step2 = new THREE.Mesh(
      new THREE.BoxGeometry(width * 0.38, rise * 0.68, depth + 0.2),
      material,
    );
    step2.position.set(width * 0.26, rise * 0.34, 0);
    const step3 = new THREE.Mesh(
      new THREE.BoxGeometry(width * 0.28, rise * 0.45, depth + 0.2),
      material,
    );
    step3.position.set(width * 0.08, rise * 0.22, 0);
    roofGroup.add(step, step2, step3);
    return roofGroup;
  }

  const gable = new THREE.Mesh(gableGeometry(width + 0.2, depth + 0.16, rise), material);
  roofGroup.add(gable);
  return roofGroup;
}

function gableGeometry(width, depth, rise) {
  const hw = width / 2;
  const hd = depth / 2;
  const positions = new Float32Array([
    -hw, 0, hd,
    hw, 0, hd,
    0, rise, hd,
    -hw, 0, -hd,
    hw, 0, -hd,
    0, rise, -hd,
  ]);
  const indices = [
    0, 1, 2,
    3, 5, 4,
    0, 2, 5, 0, 5, 3,
    1, 4, 5, 1, 5, 2,
    0, 3, 4, 0, 4, 1,
  ];
  const geometry = new THREE.BufferGeometry();
  geometry.setAttribute("position", new THREE.BufferAttribute(positions, 3));
  geometry.setIndex(indices);
  geometry.computeVertexNormals();
  return geometry;
}

function makeFacadeTexture(wallHex, floors) {
  const canvas = document.createElement("canvas");
  canvas.width = 192;
  canvas.height = 96 * Math.max(floors, 2);
  const ctx = canvas.getContext("2d");
  ctx.fillStyle = cssHex(wallHex);
  ctx.fillRect(0, 0, canvas.width, canvas.height);

  const rows = Math.max(floors, 2) * 2;
  const cols = 3;
  for (let row = 0; row < rows; row += 1) {
    for (let col = 0; col < cols; col += 1) {
      const x = 18 + col * 36;
      const y = 16 + row * 30;
      ctx.fillStyle = "#243044";
      ctx.fillRect(x, y, 22, 28);
      ctx.fillStyle = row % 2 === 0 ? "rgba(255, 214, 160, 0.62)" : "rgba(186, 214, 232, 0.42)";
      ctx.fillRect(x + 3, y + 3, 16, 10);
      ctx.strokeStyle = "rgba(255,255,255,0.12)";
      ctx.strokeRect(x + 0.5, y + 0.5, 21, 27);
    }
  }

  const texture = new THREE.CanvasTexture(canvas);
  texture.colorSpace = THREE.SRGBColorSpace;
  texture.anisotropy = 4;
  return texture;
}

function cssHex(hex) {
  return `#${hex.toString(16).padStart(6, "0")}`;
}
