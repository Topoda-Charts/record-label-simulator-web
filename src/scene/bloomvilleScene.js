import * as THREE from "three";
import { Palette } from "../config/palette.js";
import { createDistrictBuilding } from "./buildingModule.js";

/** 1 unit = 1 m · 24 m lots · 12 m street. */
const LOT_M = 24;
const STREET_M = 12;
const SIDEWALK_M = 2;
const MAIN_SPAN_M = LOT_M * 4 + STREET_M;
const SKY_COLOR = 0xd7e2f0;
const ORTHO_HALF_H = 40;

export function createBloomvilleScene(canvas) {
  const renderer = new THREE.WebGLRenderer({ canvas, antialias: true, alpha: false });
  renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
  renderer.shadowMap.enabled = true;
  renderer.shadowMap.type = THREE.PCFSoftShadowMap;
  renderer.setClearColor(SKY_COLOR, 1);
  renderer.toneMapping = THREE.ACESFilmicToneMapping;
  renderer.toneMappingExposure = 1.08;

  const scene = new THREE.Scene();
  scene.background = new THREE.Color(SKY_COLOR);
  /** Fog starts beyond the built slice so the street stays crisp. */
  scene.fog = new THREE.Fog(SKY_COLOR, 150, 215);

  const viewTarget = new THREE.Vector3(0, 7, -1);
  const camera = new THREE.OrthographicCamera(-72, 72, 40, -40, 0.5, 260);
  camera.position.set(56, 54, 70);
  camera.up.set(0, 1, 0);
  camera.lookAt(viewTarget);
  camera.updateProjectionMatrix();

  const hemi = new THREE.HemisphereLight(0xf0e8f8, 0x7a7268, 0.6);
  scene.add(hemi);

  const fill = new THREE.DirectionalLight(0xe8dce8, 0.34);
  fill.position.set(-28, 38, 18);
  scene.add(fill);

  const sun = new THREE.DirectionalLight(0xfff0dc, 1.0);
  sun.position.set(32, 48, 26);
  sun.castShadow = true;
  sun.shadow.camera.left = -58;
  sun.shadow.camera.right = 58;
  sun.shadow.camera.top = 58;
  sun.shadow.camera.bottom = -58;
  sun.shadow.camera.near = 8;
  sun.shadow.camera.far = 130;
  sun.shadow.mapSize.set(2048, 2048);
  sun.shadow.radius = 2.5;
  sun.shadow.bias = -0.00022;
  scene.add(sun);

  const districtW = MAIN_SPAN_M + 10;
  const districtD = LOT_M * 2 + STREET_M + SIDEWALK_M * 2 + 8;
  const ground = new THREE.Mesh(
    new THREE.PlaneGeometry(districtW, districtD),
    new THREE.MeshStandardMaterial({
      color: Palette.plaza,
      roughness: 0.88,
      metalness: 0.02,
    }),
  );
  ground.rotation.x = -Math.PI / 2;
  ground.receiveShadow = true;
  scene.add(ground);

  addMainStreet(scene);
  addCityHallFrontage(scene);
  const mistDrift = addUnbuiltAreaCloudEdge(scene);

  const northZ = -(STREET_M / 2 + SIDEWALK_M + LOT_M / 2);
  const southZ = STREET_M / 2 + SIDEWALK_M + LOT_M / 2;
  const lotCentersX = [-36, -12, 12, 36];

  const buildings = [];
  const northLayout = [
    { x: -36, floors: 2, roofKind: "gable", roofRise: 2.4, w: 20, d: 20, wall: Palette.structureWall, roof: Palette.structureRoofWarm },
    { x: -12, floors: 3, roofKind: "gable", roofRise: 2.8, w: 20, d: 20, wall: Palette.structureWallLilac, roof: Palette.structureRoof },
    { x: 12, floors: 4, roofKind: "flat", roofRise: 0.45, w: 22, d: 20, wall: Palette.stone, roof: Palette.structureRoof },
    { x: 36, floors: 3, roofKind: "sawtooth", roofRise: 2.2, w: 20, d: 20, wall: Palette.structureWall, roof: Palette.structureRoofWarm },
  ];
  const southLayout = [
    { x: -36, floors: 2, roofKind: "gable", roofRise: 2.2, w: 20, d: 20, wall: Palette.structureWallLilac, roof: Palette.structureRoofWarm },
    { x: -12, floors: 3, roofKind: "flat", roofRise: 0.4, w: 22, d: 20, wall: Palette.structureWall, roof: Palette.structureRoof },
    { x: 12, floors: 2, roofKind: "sawtooth", roofRise: 2.0, w: 20, d: 20, wall: Palette.stone, roof: Palette.structureRoofWarm },
    { x: 36, floors: 3, roofKind: "gable", roofRise: 2.6, w: 20, d: 20, wall: Palette.structureWallLilac, roof: Palette.structureRoof },
  ];

  for (const slot of northLayout) {
    buildings.push(placeBuilding(scene, slot, slot.x, northZ, 0));
  }
  for (const slot of southLayout) {
    buildings.push(placeBuilding(scene, slot, slot.x, southZ, Math.PI));
  }
  buildings.push(
    placeBuilding(
      scene,
      {
        x: 0,
        floors: 3,
        roofKind: "gable",
        roofRise: 3.0,
        w: 28,
        d: 22,
        wall: Palette.stone,
        roof: Palette.structureRoof,
      },
      0,
      northZ - 11,
      0,
    ),
  );

  const trees = addSoftTrees(scene, lotCentersX, northZ, southZ);
  let activeIndex = 0;
  setActiveBuilding(0);

  let frameId = 0;
  const clock = new THREE.Clock();

  function resize() {
    const width = canvas.clientWidth;
    const height = canvas.clientHeight;
    if (width === 0 || height === 0) return;
    renderer.setSize(width, height, false);
    const aspect = width / height;
    camera.left = -ORTHO_HALF_H * aspect;
    camera.right = ORTHO_HALF_H * aspect;
    camera.top = ORTHO_HALF_H;
    camera.bottom = -ORTHO_HALF_H;
    camera.updateProjectionMatrix();
    camera.lookAt(viewTarget);
  }

  function animate() {
    frameId = requestAnimationFrame(animate);
    const t = clock.getElapsedTime();
    for (let i = 0; i < trees.length; i += 1) {
      trees[i].rotation.y = Math.sin(t * 0.35 + i) * 0.012;
    }
    for (let i = 0; i < mistDrift.length; i += 1) {
      const entry = mistDrift[i];
      entry.mesh.position.y = entry.baseY + Math.sin(t * 0.15 + entry.phase) * 0.25;
      entry.mesh.material.opacity =
        entry.baseOpacity + Math.sin(t * 0.22 + entry.phase) * 0.03;
    }
    renderer.render(scene, camera);
  }

  resize();
  requestAnimationFrame(resize);
  animate();

  const ro = new ResizeObserver(resize);
  ro.observe(canvas);
  window.addEventListener("resize", resize);

  function setActiveBuilding(index) {
    activeIndex = ((index % buildings.length) + buildings.length) % buildings.length;
    buildings.forEach((b, i) => b.setActive(i === activeIndex));
  }

  return {
    setActiveBuilding,
    getActiveBuildingIndex: () => activeIndex,
    dispose() {
      cancelAnimationFrame(frameId);
      ro.disconnect();
      window.removeEventListener("resize", resize);
      renderer.dispose();
    },
  };
}

function placeBuilding(scene, slot, x, z, rotY) {
  const built = createDistrictBuilding({
    width: slot.w,
    depth: slot.d,
    floors: slot.floors,
    roofKind: slot.roofKind,
    roofRise: slot.roofRise,
    wallColor: slot.wall,
    roofColor: slot.roof,
    windowGlow: 0.3,
    floorHeight: 3.2,
  });
  built.group.position.set(x, 0, z);
  built.group.rotation.y = rotY;
  scene.add(built.group);
  return built;
}

function addMainStreet(scene) {
  const roadMat = new THREE.MeshStandardMaterial({
    color: Palette.road,
    roughness: 0.92,
    metalness: 0.02,
  });
  const walkMat = new THREE.MeshStandardMaterial({
    color: Palette.sidewalk,
    roughness: 0.84,
    metalness: 0.02,
  });
  const laneMat = new THREE.MeshStandardMaterial({
    color: 0x8a847c,
    roughness: 0.9,
    metalness: 0.01,
  });

  const road = new THREE.Mesh(new THREE.PlaneGeometry(MAIN_SPAN_M, STREET_M), roadMat);
  road.rotation.x = -Math.PI / 2;
  road.position.set(0, 0.03, 0);
  road.receiveShadow = true;
  scene.add(road);

  const centerLine = new THREE.Mesh(new THREE.PlaneGeometry(MAIN_SPAN_M, 0.35), laneMat);
  centerLine.rotation.x = -Math.PI / 2;
  centerLine.position.set(0, 0.032, 0);
  scene.add(centerLine);

  for (const zSign of [-1, 1]) {
    const walk = new THREE.Mesh(new THREE.PlaneGeometry(MAIN_SPAN_M, SIDEWALK_M), walkMat);
    walk.rotation.x = -Math.PI / 2;
    walk.position.set(0, 0.035, zSign * (STREET_M / 2 + SIDEWALK_M / 2));
    walk.receiveShadow = true;
    scene.add(walk);
  }
}

function addCityHallFrontage(scene) {
  const plazaMat = new THREE.MeshStandardMaterial({
    color: Palette.plaza,
    roughness: 0.78,
    metalness: 0.03,
  });
  const plaza = new THREE.Mesh(new THREE.PlaneGeometry(34, 16), plazaMat);
  plaza.rotation.x = -Math.PI / 2;
  plaza.position.set(0, 0.04, -(STREET_M / 2 + 11));
  plaza.receiveShadow = true;
  scene.add(plaza);

  const steps = new THREE.Mesh(new THREE.BoxGeometry(18, 0.45, 4), plazaMat);
  steps.position.set(0, 0.22, -(STREET_M / 2 + 5.5));
  steps.castShadow = true;
  steps.receiveShadow = true;
  scene.add(steps);
}

function addSoftTrees(scene, lotCentersX, northZ, southZ) {
  const trunkMat = new THREE.MeshStandardMaterial({ color: 0x6b5d52, roughness: 0.9 });
  const leafMat = new THREE.MeshStandardMaterial({
    color: Palette.anngloraFlora,
    roughness: 0.75,
    metalness: 0.02,
  });
  const trees = [];
  for (const x of lotCentersX) {
    for (const z of [northZ + LOT_M / 2 + 1.5, southZ - LOT_M / 2 - 1.5]) {
      const tree = new THREE.Group();
      const trunk = new THREE.Mesh(new THREE.CylinderGeometry(0.35, 0.5, 2.2, 6), trunkMat);
      trunk.position.y = 1.1;
      trunk.castShadow = true;
      const crown = new THREE.Mesh(new THREE.ConeGeometry(1.8, 3.6, 7), leafMat);
      crown.position.y = 3.4;
      crown.castShadow = true;
      tree.add(trunk, crown);
      tree.position.set(x + 7, 0, z);
      scene.add(tree);
      trees.push(tree);
    }
  }
  return trees;
}

/** Soft cloud/light ring — no vertical screen planes. */
function addUnbuiltAreaCloudEdge(scene) {
  const group = new THREE.Group();
  const driftables = [];
  const puffGeo = new THREE.SphereGeometry(1, 12, 10);
  const edgeX = MAIN_SPAN_M / 2 + 4;
  const edgeZ = LOT_M + STREET_M / 2 + SIDEWALK_M + 6;

  for (let i = 0; i < 28; i += 1) {
    const t = (i / 28) * Math.PI * 2;
    const wobble = 1 + (i % 5) * 0.08;
    const x = Math.cos(t) * edgeX * wobble;
    const z = Math.sin(t) * edgeZ * wobble;
    const mat = new THREE.MeshBasicMaterial({
      color: i % 3 === 0 ? 0xf8f2fa : 0xe8eef6,
      transparent: true,
      opacity: 0.1 + (i % 4) * 0.025,
      depthWrite: false,
    });
    const puff = new THREE.Mesh(puffGeo, mat);
    const s = 2.2 + (i % 6) * 0.55;
    puff.scale.set(s * 1.4, s * 0.55, s);
    puff.position.set(x, 0.8 + (i % 4) * 0.6, z);
    group.add(puff);
    driftables.push({
      mesh: puff,
      phase: i * 0.65,
      baseOpacity: mat.opacity,
      baseY: puff.position.y,
    });
  }

  scene.add(group);
  return driftables;
}
