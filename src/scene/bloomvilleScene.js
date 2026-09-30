import * as THREE from "three";
import { Palette } from "../config/palette.js";
import { createDistrictBuilding } from "./buildingModule.js";

const SKY_COLOR = 0xc8d4e8;
const FOG_NEAR = 22;
const FOG_FAR = 72;

export function createBloomvilleScene(canvas) {
  const renderer = new THREE.WebGLRenderer({ canvas, antialias: true, alpha: false });
  renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
  renderer.shadowMap.enabled = true;
  renderer.shadowMap.type = THREE.PCFSoftShadowMap;
  renderer.setClearColor(SKY_COLOR, 1);
  renderer.toneMapping = THREE.ACESFilmicToneMapping;
  renderer.toneMappingExposure = 1.05;

  const scene = new THREE.Scene();
  scene.background = new THREE.Color(SKY_COLOR);
  scene.fog = new THREE.Fog(SKY_COLOR, FOG_NEAR, FOG_FAR);

  const camera = new THREE.PerspectiveCamera(42, 1, 0.2, 120);
  camera.position.set(11.5, 7.2, 13.5);
  camera.lookAt(0, 1.4, -0.5);

  const hemi = new THREE.HemisphereLight(0xe8eef8, 0x5a5248, 0.62);
  scene.add(hemi);

  const sun = new THREE.DirectionalLight(0xfff0dc, 1.15);
  sun.position.set(14, 20, 10);
  sun.castShadow = true;
  sun.shadow.camera.left = -18;
  sun.shadow.camera.right = 18;
  sun.shadow.camera.top = 18;
  sun.shadow.camera.bottom = -18;
  sun.shadow.camera.near = 4;
  sun.shadow.camera.far = 48;
  sun.shadow.mapSize.set(2048, 2048);
  sun.shadow.bias = -0.00025;
  scene.add(sun);
  scene.add(sun.target);
  sun.target.position.set(0, 0, 0);

  const ground = new THREE.Mesh(
    new THREE.PlaneGeometry(90, 90),
    new THREE.MeshStandardMaterial({
      color: Palette.groundTint,
      roughness: 0.94,
      metalness: 0.02,
    }),
  );
  ground.rotation.x = -Math.PI / 2;
  ground.receiveShadow = true;
  scene.add(ground);

  const plaza = new THREE.Mesh(
    new THREE.CircleGeometry(5.5, 40),
    new THREE.MeshStandardMaterial({
      color: Palette.plaza,
      roughness: 0.88,
      metalness: 0.03,
    }),
  );
  plaza.rotation.x = -Math.PI / 2;
  plaza.position.y = 0.015;
  plaza.receiveShadow = true;
  scene.add(plaza);

  const buildings = [];
  const layout = [
    { x: -5.5, z: -3.5, floors: 2, roofKind: "gable", roofRise: 0.75, w: 2.3, d: 2.1 },
    { x: -2.2, z: -5.2, floors: 3, roofKind: "sawtooth", roofRise: 0.9, w: 2.5, d: 2.4 },
    { x: 1.8, z: -4.8, floors: 2, roofKind: "flat", roofRise: 0.2, w: 2.8, d: 2.2 },
    { x: 5.2, z: -2.8, floors: 4, roofKind: "gable", roofRise: 1.05, w: 2.4, d: 2.6 },
    { x: -4.2, z: 1.2, floors: 3, roofKind: "gable", roofRise: 0.95, w: 2.6, d: 2.3 },
    { x: -0.6, z: 2.4, floors: 2, roofKind: "flat", roofRise: 0.18, w: 3.2, d: 2.5 },
    { x: 3.4, z: 1.6, floors: 3, roofKind: "sawtooth", roofRise: 0.85, w: 2.5, d: 2.2 },
    { x: 6.0, z: 3.8, floors: 2, roofKind: "gable", roofRise: 0.7, w: 2.2, d: 2.0 },
  ];

  for (const slot of layout) {
    const built = createDistrictBuilding({
      width: slot.w,
      depth: slot.d,
      floors: slot.floors,
      roofKind: slot.roofKind,
      roofRise: slot.roofRise,
      wallColor: Palette.structureWall,
      roofColor: Palette.structureRoof,
    });
    built.group.position.set(slot.x, 0, slot.z);
    scene.add(built.group);
    buildings.push(built);
  }

  const trees = addSoftTrees(scene);
  let activeIndex = 0;
  setActiveBuilding(0);

  let frameId = 0;
  const clock = new THREE.Clock();

  function resize() {
    const width = canvas.clientWidth;
    const height = canvas.clientHeight;
    if (width === 0 || height === 0) return;
    renderer.setSize(width, height, false);
    camera.aspect = width / height;
    camera.updateProjectionMatrix();
  }

  function animate() {
    frameId = requestAnimationFrame(animate);
    const t = clock.getElapsedTime();
    for (let i = 0; i < trees.length; i += 1) {
      trees[i].rotation.y = Math.sin(t * 0.35 + i) * 0.02;
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

function addSoftTrees(scene) {
  const trunkMat = new THREE.MeshStandardMaterial({ color: 0x6b5d52, roughness: 0.9 });
  const leafMat = new THREE.MeshStandardMaterial({
    color: Palette.anngloraFlora,
    roughness: 0.75,
    metalness: 0.02,
  });
  const trees = [];
  const spots = [
    [-7.5, -1.5],
    [-7, 4.5],
    [7.5, -0.5],
    [8, 5],
    [-1, 6.2],
  ];
  for (const [x, z] of spots) {
    const tree = new THREE.Group();
    const trunk = new THREE.Mesh(new THREE.CylinderGeometry(0.12, 0.16, 0.9, 8), trunkMat);
    trunk.position.y = 0.45;
    trunk.castShadow = true;
    const crown = new THREE.Mesh(new THREE.SphereGeometry(0.55, 10, 8), leafMat);
    crown.position.y = 1.15;
    crown.castShadow = true;
    tree.add(trunk, crown);
    tree.position.set(x, 0, z);
    scene.add(tree);
    trees.push(tree);
  }
  return trees;
}
