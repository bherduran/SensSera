import * as THREE from "three";
import { RoundedBoxGeometry } from "three/examples/jsm/geometries/RoundedBoxGeometry.js";
import { RoomEnvironment } from "three/examples/jsm/environments/RoomEnvironment.js";

/**
 * The live clay greenhouse on the sign-in screen. Framework-free: the React component owns the
 * canvas lifecycle and calls `resize`, `setTheme`, `setPointer`, `setHover`, `drag*` and `frame`.
 * Same model as scripts/hero/render.html (which produces the static fallback images).
 */

export type HeroTheme = "light" | "dark";

type Palette = {
  plinth: string; clay: string; frame: string; bed: string; soil: string; leaf: string;
  leafLight: string; pot: string; pebble: string; glass: string;
  hemi: number; hemiGround: string; key: number; fill: string; fillI: number; shadow: number;
  env: number; lamp: number; rim: number; led: number;
};

const PALETTES: Record<HeroTheme, Palette> = {
  light: {
    plinth: "#D8C7AA", clay: "#E9DEC9", frame: "#E6D8BF", bed: "#BD7355", soil: "#6F4F3D",
    leaf: "#7F9669", leafLight: "#9AAE85", pot: "#B9694B", pebble: "#CDBB9D", glass: "#f6f8f2",
    hemi: 0.85, hemiGround: "#d6c9b3", key: 2.3, fill: "#f2ece2", fillI: 0.6, shadow: 0.24,
    env: 0.45, lamp: 0, rim: 0, led: 1.4,
  },
  dark: {
    plinth: "#6F655A", clay: "#D8CEBE", frame: "#CFC3B0", bed: "#AE6F53", soil: "#5E4535",
    leaf: "#7D926A", leafLight: "#95A882", pot: "#C0704F", pebble: "#8A7F72", glass: "#e7d9c5",
    hemi: 0.3, hemiGround: "#2a2520", key: 1.4, fill: "#6d7a8a", fillI: 0.35, shadow: 0.5,
    env: 0.16, lamp: 9, rim: 1.1, led: 3.2,
  },
};

// Hand-pressed clay: low-amplitude value noise used as bump + roughness map.
function clayNoise(size = 256, cells = 32, seed = 7): THREE.CanvasTexture {
  const canvas = document.createElement("canvas");
  canvas.width = canvas.height = size;
  const ctx = canvas.getContext("2d")!;
  const img = ctx.createImageData(size, size);
  let s = seed;
  const rnd = () => (s = (s * 16807) % 2147483647) / 2147483647;
  const grid = Array.from({ length: cells + 1 }, () => Array.from({ length: cells + 1 }, rnd));
  const ease = (a: number, b: number, t: number) => a + (b - a) * (t * t * (3 - 2 * t));
  for (let y = 0; y < size; y++) {
    for (let x = 0; x < size; x++) {
      const gx = (x / size) * cells, gy = (y / size) * cells;
      const x0 = Math.floor(gx), y0 = Math.floor(gy);
      const v = ease(
        ease(grid[y0][x0], grid[y0][x0 + 1], gx - x0),
        ease(grid[y0 + 1][x0], grid[y0 + 1][x0 + 1], gx - x0),
        gy - y0,
      );
      const n = Math.round((v * 0.82 + rnd() * 0.18) * 255);
      const i = (y * size + x) * 4;
      img.data[i] = img.data[i + 1] = img.data[i + 2] = n;
      img.data[i + 3] = 255;
    }
  }
  ctx.putImageData(img, 0, 0);
  const tex = new THREE.CanvasTexture(canvas);
  tex.wrapS = tex.wrapT = THREE.RepeatWrapping;
  tex.repeat.set(3, 3);
  return tex;
}

const clamp01 = (v: number) => Math.min(1, Math.max(0, v));
const easeOutBack = (x: number) => 1 + 2.2 * (x - 1) ** 3 + 1.2 * (x - 1) ** 2;

function roundedRect(w: number, d: number, r: number): THREE.Shape {
  const s = new THREE.Shape();
  s.moveTo(-w / 2 + r, -d / 2);
  s.lineTo(w / 2 - r, -d / 2);
  s.quadraticCurveTo(w / 2, -d / 2, w / 2, -d / 2 + r);
  s.lineTo(w / 2, d / 2 - r);
  s.quadraticCurveTo(w / 2, d / 2, w / 2 - r, d / 2);
  s.lineTo(-w / 2 + r, d / 2);
  s.quadraticCurveTo(-w / 2, d / 2, -w / 2, d / 2 - r);
  s.lineTo(-w / 2, -d / 2 + r);
  s.quadraticCurveTo(-w / 2, -d / 2, -w / 2 + r, -d / 2);
  return s;
}

export class GreenhouseScene {
  private readonly renderer: THREE.WebGLRenderer;
  private readonly scene = new THREE.Scene();
  private readonly camera = new THREE.PerspectiveCamera(24, 1, 0.1, 100);
  private readonly root = new THREE.Group();
  private readonly bump = clayNoise();
  private readonly mats: Record<string, THREE.MeshStandardMaterial> = {};
  private readonly glass: THREE.MeshPhysicalMaterial;
  private readonly led: THREE.MeshStandardMaterial;
  private readonly plants: { group: THREE.Group; phase: number; size: number; delay: number }[] = [];
  private door!: THREE.Group;
  private pollen!: THREE.Points;
  private pollenSpeed!: Float32Array;
  // Interaction state: hover eases in/out; drag spins the model, inertia carries it after release.
  private hoverTarget = 0;
  private hover = 0;
  private dragging = false;
  private yaw = 0;
  private yawVelocity = 0;
  private lastT = 0;
  private readonly hemi: THREE.HemisphereLight;
  private readonly key: THREE.DirectionalLight;
  private readonly fill: THREE.DirectionalLight;
  private readonly lamp: THREE.PointLight;
  private readonly rim: THREE.DirectionalLight;
  private readonly floorMat = new THREE.ShadowMaterial();
  private readonly pointer = new THREE.Vector2();
  private readonly pointerTarget = new THREE.Vector2();
  private palette: Palette = PALETTES.light;
  /** Camera distance along a fixed 3/4 direction; wider for narrow canvases so nothing clips. */
  private distance = 15;
  private static readonly VIEW_DIR = new THREE.Vector3(8.4, 6.5, 10.1).normalize();

  constructor(canvas: HTMLCanvasElement, theme: HeroTheme) {
    this.renderer = new THREE.WebGLRenderer({ canvas, antialias: true, alpha: true });
    this.renderer.setClearColor(0x000000, 0);
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    this.renderer.shadowMap.enabled = true;
    this.renderer.shadowMap.type = THREE.PCFSoftShadowMap;
    this.renderer.toneMapping = THREE.ACESFilmicToneMapping;
    this.renderer.toneMappingExposure = 0.98;
    this.renderer.outputColorSpace = THREE.SRGBColorSpace;

    const pmrem = new THREE.PMREMGenerator(this.renderer);
    this.scene.environment = pmrem.fromScene(new RoomEnvironment(), 0.04).texture;
    pmrem.dispose();

    this.hemi = new THREE.HemisphereLight("#fffaf2", "#d6c9b3", 1);
    this.key = new THREE.DirectionalLight("#fff3e2", 2);
    this.key.position.set(-5.5, 8.5, 5.5);
    this.key.castShadow = true;
    this.key.shadow.mapSize.set(2048, 2048);
    this.key.shadow.radius = 6;
    this.key.shadow.bias = -0.0003;
    this.key.shadow.normalBias = 0.02;
    Object.assign(this.key.shadow.camera, { left: -5, right: 5, top: 5, bottom: -5, near: 1, far: 25 });
    this.fill = new THREE.DirectionalLight("#f2ece2", 0.6);
    this.fill.position.set(6, 3, 4);
    // Night: the greenhouse is lit from inside.
    this.lamp = new THREE.PointLight("#ffb07a", 0, 5.5, 1.6);
    this.lamp.position.set(0, 1.55, 0);
    this.rim = new THREE.DirectionalLight("#e9a58a", 0);
    this.rim.position.set(5, 4, -6);
    this.scene.add(this.hemi, this.key, this.fill, this.lamp, this.rim, this.root);

    const floor = new THREE.Mesh(new THREE.PlaneGeometry(40, 40), this.floorMat);
    floor.rotation.x = -Math.PI / 2;
    floor.receiveShadow = true;
    this.root.add(floor);

    this.glass = new THREE.MeshPhysicalMaterial({
      roughness: 0.15, transparent: true, opacity: 0.32, metalness: 0, clearcoat: 0.6, clearcoatRoughness: 0.2,
      depthWrite: false,
    });
    this.led = new THREE.MeshStandardMaterial({ color: "#E88A5E", emissive: "#F08A55", roughness: 0.3 });

    this.build();
    this.setTheme(theme);
  }

  private mat(name: keyof Palette, roughness = 0.86, bumpScale = 1.4): THREE.MeshStandardMaterial {
    const key = `${String(name)}:${roughness}:${bumpScale}`;
    return (this.mats[key] ??= Object.assign(
      new THREE.MeshStandardMaterial({ roughness, metalness: 0, bumpMap: this.bump, bumpScale, roughnessMap: this.bump }),
      { name: String(name) },
    ));
  }

  private add(geo: THREE.BufferGeometry, mat: THREE.Material, parent: THREE.Object3D,
    [x, y, z]: [number, number, number],
    opts: { rot?: [number, number, number]; scale?: [number, number, number]; cast?: boolean } = {}) {
    const m = new THREE.Mesh(geo, mat);
    m.position.set(x, y, z);
    if (opts.rot) m.rotation.set(...opts.rot);
    if (opts.scale) m.scale.set(...opts.scale);
    m.castShadow = opts.cast ?? true;
    m.receiveShadow = true;
    parent.add(m);
    return m;
  }

  private build() {
    const r = this.root;
    const plinthGeo = new THREE.ExtrudeGeometry(roundedRect(5.6, 4.3, 1.1), {
      depth: 0.1, bevelEnabled: true, bevelThickness: 0.12, bevelSize: 0.14, bevelSegments: 8, curveSegments: 32,
    });
    this.add(plinthGeo, this.mat("plinth", 0.95, 1.8), r, [0.25, 0.22, 0.35], { rot: [-Math.PI / 2, 0, 0] });
    const top = 0.44;

    const pebble = this.mat("pebble", 0.9, 1.2);
    for (const [x, z, rad] of [[-2.2, 1.9, 0.11], [-1.95, 2.15, 0.07], [2.4, -1.2, 0.09], [2.65, -0.95, 0.06],
      [-2.35, -1.3, 0.08], [1.1, 2.25, 0.07], [1.35, 2.1, 0.05]]) {
      this.add(new THREE.SphereGeometry(rad, 16, 10), pebble, r, [x, top + rad * 0.45, z], { scale: [1.2, 0.7, 1] });
    }

    // Greenhouse
    const gh = new THREE.Group();
    gh.position.y = top;
    r.add(gh);
    const L = 3.4, D = 2.3, wallH = 1.3, rise = 0.9, post = 0.11, base = 0.14;
    const frame = this.mat("frame", 0.8, 1.0);
    const beam = (w: number, h: number, d: number, rad = 0.045) => new RoundedBoxGeometry(w, h, d, 3, rad);

    this.add(new RoundedBoxGeometry(L + 0.3, 0.14, D + 0.3, 4, 0.06), this.mat("clay"), gh, [0, 0.07, 0]);
    const xs = [-L / 2, -L / 6, L / 6, L / 2];
    for (const x of xs) for (const z of [-D / 2, D / 2]) this.add(beam(post, wallH, post), frame, gh, [x, base + wallH / 2, z]);
    for (const x of [-L / 2, L / 2]) this.add(beam(post, wallH, post), frame, gh, [x, base + wallH / 2, 0]);
    for (const z of [-D / 2, D / 2]) {
      this.add(beam(L + post, post, post), frame, gh, [0, base + wallH, z]);
      this.add(beam(L + post, post * 0.8, post * 0.8, 0.035), frame, gh, [0, base + 0.42, z]);
    }
    for (const x of [-L / 2, L / 2]) {
      this.add(beam(post, post, D), frame, gh, [x, base + wallH, 0]);
      this.add(beam(post * 0.8, post * 0.8, D, 0.035), frame, gh, [x, base + 0.42, 0]);
    }
    this.add(beam(L + 0.24, post * 1.3, post * 1.3, 0.055), frame, gh, [0, base + wallH + rise, 0]);
    const rafter = Math.hypot(D / 2, rise), pitch = Math.atan2(rise, D / 2);
    for (const x of xs) for (const s of [-1, 1]) {
      this.add(beam(post, post, rafter + 0.05), frame, gh, [x, base + wallH + rise / 2, (s * D) / 4], { rot: [s * pitch, 0, 0] });
    }
    // door, hinged on its back post so it can swing open on hover
    const door = (this.door = new THREE.Group());
    door.position.set(L / 2 + 0.01, base, -0.36);
    gh.add(door);
    for (const z of [0, 0.72]) this.add(beam(0.08, 1.12, 0.08, 0.035), frame, door, [0, 0.56, z]);
    this.add(beam(0.08, 0.08, 0.8, 0.035), frame, door, [0, 1.12, 0.36]);
    this.add(new THREE.BoxGeometry(0.012, 1.02, 0.66), this.glass, door, [0, 0.56, 0.36], { cast: false });
    this.add(new THREE.SphereGeometry(0.045, 14, 10), this.mat("pot", 0.6, 0.5), door, [0.06, 0.6, 0.62]);

    // pollen drifting up through the warm air inside
    const count = 70;
    const pos = new Float32Array(count * 3);
    this.pollenSpeed = new Float32Array(count);
    for (let i = 0; i < count; i++) {
      pos[i * 3] = (Math.random() - 0.5) * (L - 0.4);
      pos[i * 3 + 1] = 0.5 + Math.random() * 1.8;
      pos[i * 3 + 2] = (Math.random() - 0.5) * (D - 0.3);
      this.pollenSpeed[i] = 0.08 + Math.random() * 0.16;
    }
    const pollenGeo = new THREE.BufferGeometry();
    pollenGeo.setAttribute("position", new THREE.BufferAttribute(pos, 3));
    this.pollen = new THREE.Points(pollenGeo, new THREE.PointsMaterial({
      color: "#f2cf8f", size: 0.045, transparent: true, opacity: 0.85, depthWrite: false, sizeAttenuation: true,
    }));
    gh.add(this.pollen);
    // glass
    const pane = { cast: false };
    for (const z of [-D / 2, D / 2]) this.add(new THREE.BoxGeometry(L, wallH, 0.015), this.glass, gh, [0, base + wallH / 2, z], pane);
    for (const x of [-L / 2, L / 2]) this.add(new THREE.BoxGeometry(0.015, wallH, D), this.glass, gh, [x, base + wallH / 2, 0], pane);
    for (const s of [-1, 1]) {
      this.add(new THREE.BoxGeometry(L, 0.015, rafter), this.glass, gh, [0, base + wallH + rise / 2, (s * D) / 4], { ...pane, rot: [s * pitch, 0, 0] });
    }
    const gable = new THREE.Shape([new THREE.Vector2(-D / 2, 0), new THREE.Vector2(D / 2, 0), new THREE.Vector2(0, rise)]);
    for (const x of [-L / 2, L / 2]) {
      this.add(new THREE.ShapeGeometry(gable), this.glass, gh, [x, base + wallH, 0], { ...pane, rot: [0, Math.PI / 2, 0] });
    }

    // beds + plants
    let seed = 0;
    for (const [z, off] of [[-0.52, 0], [0.52, 0.2]]) {
      const bed = new THREE.Group();
      bed.position.set(0, base, z);
      gh.add(bed);
      this.add(new RoundedBoxGeometry(L - 0.55, 0.3, 0.62, 4, 0.08), this.mat("bed", 0.9, 1.6), bed, [0, 0.15, 0]);
      this.add(new RoundedBoxGeometry(L - 0.72, 0.05, 0.47, 3, 0.02), this.mat("soil", 0.97, 3), bed, [0, 0.3, 0]);
      for (let i = 0; i < 5; i++) this.plant(bed, [-1.2 + i * 0.6 + off, 0.3, 0], 0.85 + (i % 3) * 0.12, ++seed);
    }

    // terracotta pot
    const profile = [[0, 0], [0.3, 0], [0.34, 0.04], [0.42, 0.56], [0.48, 0.58], [0.49, 0.7], [0.45, 0.72], [0.41, 0.64], [0, 0.64]]
      .map(([x, y]) => new THREE.Vector2(x, y));
    const pot = new THREE.Group();
    pot.position.set(-2.35, top, 1.35);
    pot.scale.setScalar(0.8);
    r.add(pot);
    this.add(new THREE.LatheGeometry(profile, 48), this.mat("pot", 0.84, 1.4), pot, [0, 0, 0]);
    this.add(new THREE.CylinderGeometry(0.4, 0.4, 0.03, 32), this.mat("soil", 0.97, 3), pot, [0, 0.64, 0]);
    this.plant(pot, [0, 0.64, 0], 2.3, 3);

    // sensor
    const sensor = new THREE.Group();
    sensor.position.set(0.5, top, 2.05);
    sensor.rotation.y = 0.25;
    r.add(sensor);
    this.add(new RoundedBoxGeometry(0.3, 0.46, 0.2, 5, 0.09), this.mat("clay", 0.78, 0.8), sensor, [0, 0.23, 0]);
    this.add(new THREE.SphereGeometry(0.03, 16, 12), this.led, sensor, [0, 0.36, 0.1], { cast: false });
    for (let row = 0; row < 2; row++) for (let c = 0; c < 3; c++) {
      this.add(new THREE.SphereGeometry(0.012, 8, 6), this.mat("frame", 0.9, 0.3), sensor, [-0.05 + c * 0.05, 0.2 - row * 0.05, 0.1], { cast: false });
    }
  }

  // A rosette of tapered, slightly cupped leaves; kept in `plants` so frame() can sway it.
  private plant(parent: THREE.Object3D, [x, y, z]: [number, number, number], size: number, seed: number) {
    const g = new THREE.Group();
    g.position.set(x, y, z);
    g.scale.setScalar(size);
    g.rotation.y = seed * 1.7;
    parent.add(g);
    const leafGeo = new THREE.SphereGeometry(1, 18, 12);
    const n = 6 + (seed % 3);
    for (let i = 0; i < n; i++) {
      const tilt = 0.55 + ((i * 37 + seed * 11) % 10) / 30;
      const len = 0.2 + ((i * 13 + seed) % 5) * 0.025;
      const leaf = new THREE.Group();
      leaf.rotation.y = -(i / n) * Math.PI * 2;
      g.add(leaf);
      const blade = this.add(leafGeo, this.mat(i % 2 ? "leaf" : "leafLight", 0.82, 0.8), leaf, [0, 0, 0], {
        scale: [len * 0.42, len * 0.12, len],
      });
      blade.position.set(0, len * Math.cos(tilt) * 0.9, len * Math.sin(tilt) * 0.9);
      blade.rotation.x = -tilt;
    }
    this.add(leafGeo, this.mat("leafLight", 0.82, 0.8), g, [0, 0.2, 0], { scale: [0.05, 0.13, 0.05] });
    this.plants.push({ group: g, phase: seed * 0.9, size, delay: (seed % 11) * 0.07 });
  }

  setTheme(theme: HeroTheme) {
    const p = (this.palette = PALETTES[theme]);
    for (const m of Object.values(this.mats)) m.color.set(p[m.name as keyof Palette] as string);
    this.glass.color.set(p.glass);
    this.glass.opacity = theme === "light" ? 0.32 : 0.26;
    this.hemi.intensity = p.hemi;
    this.hemi.groundColor.set(p.hemiGround);
    this.key.intensity = p.key;
    this.fill.color.set(p.fill);
    this.fill.intensity = p.fillI;
    this.lamp.intensity = p.lamp;
    this.rim.intensity = p.rim;
    this.floorMat.opacity = p.shadow;
    this.scene.environmentIntensity = p.env;
  }

  resize(width: number, height: number) {
    this.renderer.setSize(width, height, false);
    this.camera.aspect = width / height;
    // The model is wider than tall: back the camera off as the canvas gets narrower.
    this.distance = THREE.MathUtils.clamp(19 / Math.min(this.camera.aspect, 1.45), 13, 26);
    this.camera.updateProjectionMatrix();
  }

  /** Normalised pointer in [-1, 1]; the model leans toward it (more while hovered). */
  setPointer(x: number, y: number) {
    this.pointerTarget.set(x, y);
  }

  /** Pointer over the model: it leans in, the door swings open, the plants get livelier. */
  setHover(on: boolean) {
    this.hoverTarget = on ? 1 : 0;
  }

  dragStart() {
    this.dragging = true;
    this.yawVelocity = 0;
  }

  /** Horizontal drag, as a fraction of the canvas width since the last move. */
  dragBy(dx: number, dt: number) {
    const delta = dx * Math.PI * 1.4;
    this.yaw += delta;
    this.yawVelocity = dt > 0 ? delta / dt : 0;
  }

  dragEnd() {
    this.dragging = false;
  }

  /** Advance the animation to time `t` (seconds) and draw. */
  frame(t: number) {
    const dt = Math.min(0.05, Math.max(0, t - this.lastT));
    this.lastT = t;
    this.pointer.lerp(this.pointerTarget, 1 - Math.exp(-dt * 4));
    this.hover += (this.hoverTarget - this.hover) * (1 - Math.exp(-dt * 5));
    const h = this.hover;

    // Spin: inertia after a drag, then an unhurried return to the resting angle.
    if (!this.dragging) {
      this.yaw += this.yawVelocity * dt;
      this.yawVelocity *= Math.exp(-dt * 2.2);
      if (Math.abs(this.yawVelocity) < 0.4) this.yaw *= Math.exp(-dt * 0.9);
    }

    // Intro: the whole model settles in with a little overshoot, then the plants sprout.
    const intro = easeOutBack(clamp01(t / 1.1));
    this.root.scale.setScalar(0.82 + 0.18 * intro);
    this.root.rotation.y =
      (1 - intro) * -0.6 + Math.sin(t * 0.22) * 0.3 * (1 - h * 0.6) + this.pointer.x * (0.2 + 0.35 * h) + this.yaw;
    this.root.rotation.x = this.pointer.y * (0.04 + 0.06 * h);
    this.root.position.y = Math.sin(t * 0.7) * 0.05 + h * 0.08;

    const swayAmp = 0.05 + 0.09 * h;
    for (const { group, phase, size, delay } of this.plants) {
      group.scale.setScalar(size * easeOutBack(clamp01((t - 0.45 - delay) / 0.55)));
      group.rotation.z = Math.sin(t * (1.1 + 0.8 * h) + phase) * swayAmp;
      group.rotation.x = Math.cos(t * (0.9 + 0.6 * h) + phase) * swayAmp * 0.6;
    }

    this.door.rotation.y = h * 1.15;

    const pos = this.pollen.geometry.getAttribute("position") as THREE.BufferAttribute;
    for (let i = 0; i < pos.count; i++) {
      let y = pos.getY(i) + this.pollenSpeed[i] * dt * (1 + h);
      if (y > 2.35) y = 0.5;
      pos.setY(i, y);
      pos.setX(i, pos.getX(i) + Math.sin(t * 0.8 + i) * dt * 0.06);
    }
    pos.needsUpdate = true;
    (this.pollen.material as THREE.PointsMaterial).opacity = (0.55 + 0.35 * h) * clamp01((t - 0.8) / 0.8);

    // Sensor LED: a short heartbeat, quicker while you're looking at it.
    const beat = Math.max(0, Math.sin(t * (2.5 + 2.5 * h))) ** 12;
    this.led.emissiveIntensity = this.palette.led * (0.35 + beat);

    this.camera.position.copy(GreenhouseScene.VIEW_DIR).multiplyScalar(this.distance * (1 - 0.05 * h));
    this.camera.position.y -= this.pointer.y * 0.3;
    // Aim slightly below the model's centre so it sits high and leaves room for the caption.
    this.camera.lookAt(0.2, 0.2, 0.4);
    this.renderer.render(this.scene, this.camera);
  }

  dispose() {
    this.scene.traverse((o) => {
      if (o instanceof THREE.Mesh) o.geometry.dispose();
    });
    for (const m of Object.values(this.mats)) m.dispose();
    this.glass.dispose();
    this.led.dispose();
    this.floorMat.dispose();
    (this.pollen.material as THREE.Material).dispose();
    this.bump.dispose();
    this.renderer.dispose();
  }
}
