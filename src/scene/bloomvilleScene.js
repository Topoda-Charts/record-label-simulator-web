import * as THREE from "three";
import { OrbitControls } from "three/examples/jsm/controls/OrbitControls.js";
import { Palette } from "../config/palette.js";

export function createBloomvilleScene(canvas) {
  const renderer = new THREE.WebGLRenderer({ canvas, antialias: true, alpha: false });
  renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
  renderer.shadowMap.enabled = true;
  renderer.shadowMap.type = THREE.PCFSoftShadowMap;
  renderer.setClearColor(0x120f18, 1);

  const scene = new THREE.Scene();
  scene.fog = new THREE.Fog(0x1a1820, 28, 95);

  const camera = new THREE.PerspectiveCamera(48, 1, 0.1, 200);
  camera.position.set(14, 11, 16);

  const controls = new OrbitControls(camera, canvas);
  controls.enableDamping = true;
  controls.target.set(0, 2, 0);
  controls.maxPolarAngle = Math.PI * 0.49;
  controls.minDistance = 8;
  controls.maxDistance = 40;

  const hemi = new THREE.HemisphereLight(0xdde8ff, 0x2a2630, 0.55);
  scene.add(hemi);

  const sun = new THREE.DirectionalLight(0xfff2e0, 1.1);
  sun.position.set(10, 18, 6);
  sun.castShadow = true;
  sun.shadow.mapSize.set(1024, 1024);
  scene.add(sun);

  const anngloraGlow = new THREE.PointLight(Palette.annglora, 0.9, 22);
  anngloraGlow.position.set(-6, 4, -4);
  scene.add(anngloraGlow);

  const byteriaGlow = new THREE.PointLight(Palette.byteria, 0.65, 18);
  byteriaGlow.position.set(8, 3.5, 2);
  scene.add(byteriaGlow);

  const crowniaBounce = new THREE.PointLight(Palette.crownia, 0.45, 16);
  crowniaBounce.position.set(2, 2.5, -9);
  scene.add(crowniaBounce);

  const ground = new THREE.Mesh(
    new THREE.PlaneGeometry(80, 80),
    new THREE.MeshStandardMaterial({ color: Palette.ground, roughness: 0.92 }),
  );
  ground.rotation.x = -Math.PI / 2;
  ground.receiveShadow = true;
  scene.add(ground);

  const plaza = new THREE.Mesh(
    new THREE.CircleGeometry(7, 48),
    new THREE.MeshStandardMaterial({ color: 0x3a3542, roughness: 0.85 }),
  );
  plaza.rotation.x = -Math.PI / 2;
  plaza.position.y = 0.02;
  plaza.receiveShadow = true;
  scene.add(plaza);

  addRoadGrid(scene);
  addCityHall(scene);
  addLabelPads(scene);

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
    anngloraGlow.intensity = 0.75 + Math.sin(t * 0.8) * 0.12;
    byteriaGlow.intensity = 0.55 + Math.sin(t * 1.1 + 1) * 0.1;
    controls.update();
    renderer.render(scene, camera);
  }

  resize();
  animate();

  const ro = new ResizeObserver(resize);
  ro.observe(canvas);

  return {
    dispose() {
      cancelAnimationFrame(frameId);
      ro.disconnect();
      controls.dispose();
      renderer.dispose();
    },
  };
}

function addRoadGrid(scene) {
  const roadMat = new THREE.MeshStandardMaterial({ color: Palette.road, roughness: 0.9 });
  for (let i = -1; i <= 1; i += 1) {
    const h = new THREE.Mesh(new THREE.BoxGeometry(36, 0.08, 1.2), roadMat);
    h.position.set(0, 0.04, i * 6);
    h.receiveShadow = true;
    scene.add(h);

    const v = new THREE.Mesh(new THREE.BoxGeometry(1.2, 0.08, 36), roadMat);
    v.position.set(i * 6, 0.04, 0);
    v.receiveShadow = true;
    scene.add(v);
  }
}

function addCityHall(scene) {
  const base = new THREE.Mesh(
    new THREE.BoxGeometry(5.5, 3.2, 5.5),
    new THREE.MeshStandardMaterial({ color: Palette.anngloraFlora, roughness: 0.55, metalness: 0.05 }),
  );
  base.position.set(0, 1.6, 0);
  base.castShadow = true;
  base.receiveShadow = true;
  scene.add(base);

  const crown = new THREE.Mesh(
    new THREE.BoxGeometry(3.2, 1.4, 3.2),
    new THREE.MeshStandardMaterial({ color: Palette.annglora, roughness: 0.4, emissive: 0x221133, emissiveIntensity: 0.35 }),
  );
  crown.position.set(0, 3.5, 0);
  crown.castShadow = true;
  scene.add(crown);
}

function addLabelPads(scene) {
  const pads = [
    { name: "ARL", color: Palette.annglora, pos: [-7, 0, -6], h: 2.8 },
    { name: "BRL", color: Palette.byteria, pos: [7, 0, 1], h: 3.4 },
    { name: "CRL", color: Palette.crownia, pos: [1, 0, -8], h: 2.5 },
  ];

  for (const pad of pads) {
    const slab = new THREE.Mesh(
      new THREE.BoxGeometry(4.2, 0.25, 4.2),
      new THREE.MeshStandardMaterial({ color: pad.color, roughness: 0.45, metalness: 0.15 }),
    );
    slab.position.set(pad.pos[0], 0.12, pad.pos[2]);
    slab.receiveShadow = true;
    scene.add(slab);

    const hq = new THREE.Mesh(
      new THREE.BoxGeometry(2.6, pad.h, 2.6),
      new THREE.MeshStandardMaterial({
        color: pad.color,
        roughness: 0.35,
        emissive: pad.color,
        emissiveIntensity: pad.name === "BRL" ? 0.25 : 0.12,
      }),
    );
    hq.position.set(pad.pos[0], pad.h / 2 + 0.25, pad.pos[2]);
    hq.castShadow = true;
    scene.add(hq);
  }
}
