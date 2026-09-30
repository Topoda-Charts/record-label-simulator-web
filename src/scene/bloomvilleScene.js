import * as THREE from "three";
import { Palette } from "../config/palette.js";
import { createDistrictBuilding } from "./buildingModule.js";

/** 1 scene unit = 1 meter (Visualizer Lab / expansion doc reconciliation). */
const LOT_M = 24;
const STREET_M = 12;
const SIDEWALK_M = 2;
const MAIN_SPAN_M = LOT_M * 4 + STREET_M;
const SKY_COLOR = 0xd7e2f0;
const FOG_NEAR = 55;
const FOG_FAR = 130;

export function createBloomvilleScene(canvas) {
  const renderer = new THREE.WebGLRenderer({ canvas, antialias: true, alpha: false });
  renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
  renderer.shadowMap.enabled = true;
  renderer.shadowMap.type = THREE.PCFSoftShadowMap;
  renderer.setClearColor(SKY_COLOR, 1);
  renderer.toneMapping = THREE.ACESFilmicToneMapping;
  renderer.toneMappingExposure = 1.06;

  const scene = new THREE.Scene();
  scene.background = new THREE.Color(SKY_COLOR);
  scene.fog = new THREE.Fog(SKY_COLOR, FOG_NEAR, FOG_FAR);

  let orthoHalfHeight = 34;
  const camera = new THREE.OrthographicCamera(-1, 1, 1, -1, 0.5, 220);
  camera.position.set(0, 92, 0);
  camera.up.set(0, 0, -1);
  camera.lookAt(0, 0, 0);

  const hemi = new THREE.HemisphereLight(0xf0e8f8, 0x6a6258, 0.62);
  scene.add(hemi);

  const fill = new THREE.DirectionalLight(0xe8dce8, 0.35);
  fill.position.set(-20, 40, -30);
  scene.add(fill);

  const sun = new THREE.DirectionalLight(0xfff0dc, 1.05);
  sun.position.set(28, 55, 22);
  sun.castShadow = true;
  sun.shadow.camera.left = -58;
  sun.shadow.camera.right = 58;
  sun.shadow.camera.top = 58;
  sun.shadow.camera.bottom = -58;
  sun.shadow.camera.near = 8;
  sun.shadow.camera.far = 120;
  sun.shadow.mapSize.set(2048, 2048);
  sun.shadow.bias = -0.0003;
  scene.add(sun);

  const ground = new THREE.Mesh(
    new THREE.PlaneGeometry(MAIN_SPAN_M + 20, LOT_M * 2 + STREET_M + SIDEWALK_M * 2 + 16),
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
    { x: -36, floors: 2, roofKind: "gable", roofRise: 1.05, w: 20, d: 20, wall: Palette.structureWall, roof: Palette.structureRoofWarm },
    { x: -12, floors: 3, roofKind: "gable", roofRise: 1.2, w: 20, d: 20, wall: Palette.structureWallLilac, roof: Palette.structureRoof },
    { x: 12, floors: 4, roofKind: "flat", roofRise: 0.35, w: 22, d: 20, wall: Palette.stone, roof: Palette.structureRoof },
    { x: 36, floors: 3, roofKind: "sawtooth", roofRise: 1.1, w: 20, d: 20, wall: Palette.structureWall, roof: Palette.structureRoofWarm },
  ];
  const southLayout = [
    { x: -36, floors: 2, roofKind: "gable", roofRise: 1.0, w: 20, d: 20, wall: Palette.structureWallLilac, roof: Palette.structureRoofWarm },
    { x: -12, floors: 3, roofKind: "flat", roofRise: 0.3, w: 22, d: 20, wall: Palette.structureWall, roof: Palette.structureRoof },
    { x: 12, floors: 2, roofKind: "sawtooth", roofRise: 1.0, w: 20, d: 20, wall: Palette.stone, roof: Palette.structureRoofWarm },
    { x: 36, floors: 3, roofKind: "gable", roofRise: 1.15, w: 20, d: 20, wall: Palette.structureWallLilac, roof: Palette.structureRoof },
  ];

  for (const slot of northLayout) {
    buildings.push(placeBuilding(scene, slot, slot.x, northZ, Math.PI));
  }
  for (const slot of southLayout) {
    buildings.push(placeBuilding(scene, slot, slot.x, southZ, 0));
  }
  buildings.push(
    placeBuilding(
      scene,
      {
        x: 0,
        floors: 3,
        roofKind: "gable",
        roofRise: 1.35,
        w: 28,
        d: 22,
        wall: Palette.stone,
        roof: Palette.structureRoof,
      },
      0,
      northZ - 8,
      Math.PI,
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
    camera.left = -orthoHalfHeight * aspect;
    camera.right = orthoHalfHeight * aspect;
    camera.top = orthoHalfHeight;
    camera.bottom = -orthoHalfHeight;
    camera.updateProjectionMatrix();
  }

  function animate() {
    frameId = requestAnimationFrame(animate);
    const t = clock.getElapsedTime();
    for (let i = 0; i < trees.length; i += 1) {
      trees[i].rotation.y = Math.sin(t * 0.35 + i) * 0.015;
    }
    for (let i = 0; i < mistDrift.length; i += 1) {
      const entry = mistDrift[i];
      entry.mesh.position.x += Math.sin(t * 0.12 + entry.phase) * 0.002;
      entry.mesh.material.opacity =
        entry.baseOpacity + Math.sin(t * 0.2 + entry.phase) * 0.03;
    }
    renderer.render(scene, camera);
  }

  resize();
  animate();

  const ro = new ResizeObserver(resize);
  ro.observe(canvas);

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
    windowGlow: 0.28,
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

  const road = new THREE.Mesh(new THREE.PlaneGeometry(MAIN_SPAN_M, STREET_M), roadMat);
  road.rotation.x = -Math.PI / 2;
  road.position.set(0, 0.03, 0);
  road.receiveShadow = true;
  scene.add(road);

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
  const plaza = new THREE.Mesh(new THREE.PlaneGeometry(32, 14), plazaMat);
  plaza.rotation.x = -Math.PI / 2;
  plaza.position.set(0, 0.04, -(STREET_M / 2 + 10));
  plaza.receiveShadow = true;
  scene.add(plaza);
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
    for (const z of [
      northZ + LOT_M / 2 + 2,
      southZ - LOT_M / 2 - 2,
    ]) {
      const tree = new THREE.Group();
      const trunk = new THREE.Mesh(new THREE.CylinderGeometry(0.35, 0.5, 2.2, 6), trunkMat);
      trunk.position.y = 1.1;
      trunk.castShadow = true;
      const crown = new THREE.Mesh(new THREE.ConeGeometry(1.8, 3.6, 7), leafMat);
      crown.position.y = 3.4;
      crown.castShadow = true;
      tree.add(trunk, crown);
      tree.position.set(x + 8, 0, z);
      scene.add(tree);
      trees.push(tree);
    }
  }
  return trees;
}

/** Cloud and soft light beyond the built Central slice (presentation edge, JL 2026-09-30). */
function addUnbuiltAreaCloudEdge(scene) {
  const group = new THREE.Group();
  const driftables = [];
  const edgeX = MAIN_SPAN_M / 2 + 8;
  const edgeZ = LOT_M + STREET_M / 2 + SIDEWALK_M + 10;

  const layerSpecs = [
    { color: 0xf6f0fa, opacity: 0.4, y: 2, scale: 1 },
    { color: 0xece6f4, opacity: 0.34, y: 6, scale: 1.03 },
    { color: 0xe0eaf2, opacity: 0.28, y: 11, scale: 1.06 },
  ];

  const wallDefs = [
    { x: 0, z: -edgeZ - 6, rotY: 0, w: MAIN_SPAN_M + 36, h: 14 },
    { x: 0, z: edgeZ + 6, rotY: 0, w: MAIN_SPAN_M + 36, h: 14 },
    { x: -edgeX - 6, z: 0, rotY: Math.PI / 2, w: edgeZ * 2 + 20, h: 14 },
    { x: edgeX + 6, z: 0, rotY: Math.PI / 2, w: edgeZ * 2 + 20, h: 14 },
  ];

  for (const wall of wallDefs) {
    for (let li = 0; li < layerSpecs.length; li += 1) {
      const spec = layerSpecs[li];
      const mat = new THREE.MeshBasicMaterial({
        color: spec.color,
        transparent: true,
        opacity: spec.opacity,
        depthWrite: false,
        side: THREE.DoubleSide,
      });
      const mesh = new THREE.Mesh(
        new THREE.PlaneGeometry(wall.w * spec.scale, wall.h + li * 2),
        mat,
      );
      mesh.position.set(wall.x, spec.y + li * 3, wall.z);
      mesh.rotation.y = wall.rotY;
      group.add(mesh);
      driftables.push({ mesh, phase: wall.x + wall.z + li * 2.1, baseOpacity: spec.opacity });
    }
  }

  const skirtMat = new THREE.MeshBasicMaterial({
    color: 0xdce6f0,
    transparent: true,
    opacity: 0.42,
    side: THREE.BackSide,
    depthWrite: false,
  });
  const skirt = new THREE.Mesh(new THREE.CylinderGeometry(62, 78, 18, 64, 1, true), skirtMat);
  skirt.position.y = 8;
  group.add(skirt);
  driftables.push({ mesh: skirt, phase: 0.5, baseOpacity: 0.42 });

  scene.add(group);
  return driftables;
}
