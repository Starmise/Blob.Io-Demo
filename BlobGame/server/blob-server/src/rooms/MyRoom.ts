import { Room, Client } from "colyseus";
import { GameState, PlayerState, BlobPickup } from "./schema/MyRoomState.js";

// Half-width of the arena in world units (players stay within [-MAP_SIZE, MAP_SIZE] on X/Z).
// Must match the Unity Game scene: Plane scale (2*MAP_SIZE+2)/10 on X/Z, walls at ±(MAP_SIZE+1).
const MAP_SIZE = 75;
const BASE_SPEED = 1;
/** Target blob count (high — use spatial grid in update for O(n) pickup checks). */
const BLOB_COUNT = 6000;
/** World units per grid cell for blob proximity queries (≈ max pickup reach / 2). */
const BLOB_CELL_SIZE = 8;
const INVINCIBLE_SEC = 10;
const BLOB_VALUES = [1, 2, 5, 10];
const SPECIAL_BLOB_CHANCE = 0.005;
const SPEED_BLOB_CHANCE = 0.01;
const SLOW_BLOB_CHANCE = 0.01;
const SPEED_BOOST_MULTIPLIER = 1.35;
/** Mirrors boost strength: move speed × this while slow debuff is active. */
const SPEED_SLOW_MULTIPLIER = 1 / SPEED_BOOST_MULTIPLIER;
const SPEED_BOOST_DURATION_SEC = 4;
// Controls how quickly players visually "grow" from score.
// This MUST match the client-side scale curve (Unity `PlayerController`).
const MAX_SCORE_FOR_SCALE = 200000;

// Movement: io-style — barely slower when huge.
const SPEED_SCORE_REF = MAX_SCORE_FOR_SCALE;
const SPEED_LOG_WEIGHT = 0.055;
// Safety floor for absurd scores; normal play stays above this via the soft curve.
const MIN_SPEED_RATIO = 0.92;

/** Center-to-center distance between split halves (smaller = easier to merge back). */
const SPLIT_SEPARATION = 1.65;
/** Merge when distance < (rA + rB) * this. */
const MERGE_DIST_FACTOR = 0.42;
/** Over this many seconds the split copy eases toward the primary (smoothstep), making re-merge easier. */
const SPLIT_PULL_DURATION_SEC = 10;

/** Pickup tint options — edit to taste; must stay in sync with client expectations. */
const BLOB_PICKUP_COLORS = [
  "#ff6b6b",
  "#4ecdc4",
  "#ffe66d",
  "#a29bfe",
  "#fd79a8",
  "#74b9ff",
] as const;

function randomBlobPresetColor(): string {
  return BLOB_PICKUP_COLORS[
    Math.floor(Math.random() * BLOB_PICKUP_COLORS.length)
  ];
}

// cd BlobGame/server/blob-server // yarn start

export class GameRoom extends Room {
  declare state: GameState;
  private readonly _speedBoostUntil = new Map<string, number>();
  private readonly _speedSlowUntil = new Map<string, number>();
  /** Split mass gradually pulls toward primary; world dir from primary→copy and initial separation are fixed at split. */
  private readonly _splitPullStart = new Map<string, number>();
  private readonly _splitInitSep = new Map<string, number>();
  private readonly _splitDirX = new Map<string, number>();
  private readonly _splitDirZ = new Map<string, number>();

  onCreate(options: any) {
    this.state = new GameState();
    this.spawnBlobs(BLOB_COUNT);

    this.setSimulationInterval((dt) => this.update(dt), 1000 / 20);

    // Movement
    this.onMessage("move", (client, data: { x: number; z: number }) => {
      this.handleMove(client.sessionId, data.x, data.z);
    });

    this.onMessage("split", (client, data: { x?: number; z?: number }) => {
      this.handleSplit(client.sessionId, data?.x ?? 0, data?.z ?? 0);
    });

    // Buy skin
    this.onMessage(
      "buySkin",
      (client, data: { skinId: string; killCost: number }) => {
        const p = this.state.players.get(client.sessionId);
        if (!p) return;
        if (p.kills < data.killCost) return;
        p.kills -= data.killCost;
        p.skinId = data.skinId;
      },
    );

    // Add score (daily bonus, gifts)
    this.onMessage("addScore", (client, data: { amount: number }) => {
      const p = this.state.players.get(client.sessionId);
      if (p) p.score += data.amount;
    });

    // Add kills (gifts)
    this.onMessage("addKills", (client, data: { amount: number }) => {
      const p = this.state.players.get(client.sessionId);
      if (p) p.kills += data.amount;
    });

    // Set color (skins)
    this.onMessage("setColor", (client, data: { color: string }) => {
      const p = this.state.players.get(client.sessionId);
      if (p) p.color = data.color;
    });

    // Request respawn (after death)
    this.onMessage("requestRespawn", (client) => {
      const p = this.state.players.get(client.sessionId);
      if (!p) return;
      p.x = (Math.random() - 0.5) * MAP_SIZE;
      p.z = (Math.random() - 0.5) * MAP_SIZE;
      p.isAlive = true;
      p.isInvincible = true;
      p.invincibilityEndTime = Date.now() / 1000 + INVINCIBLE_SEC;
      this._speedBoostUntil.delete(client.sessionId);
      this._speedSlowUntil.delete(client.sessionId);
      p.speedBoostActive = false;
      p.speedSlowActive = false;
      p.hasSplit = false;
      p.splitScore = 0;
      p.splitSize = 0;
      this.clearSplitPullData(client.sessionId);
    });
  }

  onJoin(client: Client, options: any) {
    const p = new PlayerState();
    p.id = client.sessionId;
    p.name = (options.name || "Player").substring(0, 16);
    p.skinId = options.skinId ?? "default";
    p.x = (Math.random() - 0.5) * MAP_SIZE; // Spawn in random position
    p.z = (Math.random() - 0.5) * MAP_SIZE; // Y is fixed since it's a "top-down" game
    p.y = 0;
    p.size = 1;
    p.isAlive = true;
    p.isInvincible = true;
    p.invincibilityEndTime = Date.now() / 1000 + INVINCIBLE_SEC; // Invincibility on spawn for 10 seconds
    p.speedBoostActive = false;
    p.speedSlowActive = false;
    p.hasSplit = false;
    p.splitScore = 0;
    p.splitSize = 0;
    this.state.players.set(client.sessionId, p);
    console.log(`[JOIN] ${p.name}`);
  }

  // Handle player leaving the room
  onLeave(client: Client) {
    this.state.players.delete(client.sessionId);
    this._speedBoostUntil.delete(client.sessionId);
    this._speedSlowUntil.delete(client.sessionId);
    this.clearSplitPullData(client.sessionId);
    console.log(`[LEAVE] ${client.sessionId}`);
  }

  update(dt: number) {
    const now = Date.now() / 1000;
    const blobGrid = this.buildBlobCellGrid();
    this.state.players.forEach((p: PlayerState) => {
      // Update invincibility and check pickups
      if (!p.isAlive) return;
      if (p.isInvincible && now >= p.invincibilityEndTime) {
        p.isInvincible = false;
      }
      p.x = Math.max(-MAP_SIZE, Math.min(MAP_SIZE, p.x)); // Keep player within bounds
      p.z = Math.max(-MAP_SIZE, Math.min(MAP_SIZE, p.z)); // Keep player within bounds
      if (p.hasSplit) {
        this.applySplitPull(p);
      }
      this.tryMergeSplitHalves(p);
      this.checkBlobPickups(p, blobGrid);

      const untilBoost = this._speedBoostUntil.get(p.id) ?? 0;
      const boostOn = now < untilBoost;
      if (p.speedBoostActive !== boostOn) p.speedBoostActive = boostOn;

      const untilSlow = this._speedSlowUntil.get(p.id) ?? 0;
      const slowOn = now < untilSlow;
      if (p.speedSlowActive !== slowOn) p.speedSlowActive = slowOn;
    });
    this.checkPlayerCollisions();
  }

  /** Bucket blob ids by grid cell so each player only scans nearby blobs. */
  private buildBlobCellGrid(): Map<string, string[]> {
    const grid = new Map<string, string[]>();
    this.state.blobs.forEach((blob, id) => {
      const ix = Math.floor(blob.x / BLOB_CELL_SIZE);
      const iz = Math.floor(blob.z / BLOB_CELL_SIZE);
      const k = `${ix},${iz}`;
      let bucket = grid.get(k);
      if (!bucket) {
        bucket = [];
        grid.set(k, bucket);
      }
      bucket.push(id);
    });
    return grid;
  }

  // Handle player movement input
  handleMove(sessionId: string, inputX: number, inputZ: number) {
    const p = this.state.players.get(sessionId);
    if (!p || !p.isAlive) return;
    const len = Math.sqrt(inputX * inputX + inputZ * inputZ);
    if (len === 0) return;

    const totalScore = p.hasSplit ? p.score + p.splitScore : p.score;
    const speedT = Math.log1p(totalScore / SPEED_SCORE_REF) * SPEED_LOG_WEIGHT;
    const speedMultiplier = 1 / (1 + speedT);
    let speed = Math.max(
      BASE_SPEED * speedMultiplier,
      BASE_SPEED * MIN_SPEED_RATIO,
    );
    const boostUntil = this._speedBoostUntil.get(sessionId) ?? 0;
    if (Date.now() / 1000 < boostUntil) {
      speed *= SPEED_BOOST_MULTIPLIER;
    }
    const slowUntil = this._speedSlowUntil.get(sessionId) ?? 0;
    if (Date.now() / 1000 < slowUntil) {
      speed *= SPEED_SLOW_MULTIPLIER;
    }
    const dt = 1 / 20;
    const dx = (inputX / len) * speed * dt;
    const dz = (inputZ / len) * speed * dt;
    p.x += dx;
    p.z += dz;
    p.x = Math.max(-MAP_SIZE, Math.min(MAP_SIZE, p.x));
    p.z = Math.max(-MAP_SIZE, Math.min(MAP_SIZE, p.z));
    if (p.hasSplit) {
      this.applySplitPull(p);
    }
  }

  private handleSplit(sessionId: string, dirX: number, dirZ: number) {
    const p = this.state.players.get(sessionId);
    if (!p || !p.isAlive || p.hasSplit) return;
    if (p.score < 2) return;
    const total = p.score;
    const half = Math.floor(total / 2);
    if (half < 1) return;

    p.score = half;
    p.splitScore = half;
    const sz = p.size;
    p.size = sz * 0.5;
    p.splitSize = sz * 0.5;

    const cx = p.x;
    const cz = p.z;
    const len = Math.hypot(dirX, dirZ);
    let ox: number;
    let oz: number;
    if (len > 1e-6) {
      ox = (dirX / len) * SPLIT_SEPARATION * 0.5;
      oz = (dirZ / len) * SPLIT_SEPARATION * 0.5;
    } else {
      const angle = Math.random() * Math.PI * 2;
      ox = Math.cos(angle) * SPLIT_SEPARATION * 0.5;
      oz = Math.sin(angle) * SPLIT_SEPARATION * 0.5;
    }
    // Primary stays behind, copy launches along facing (+dir)
    p.x = cx - ox;
    p.z = cz - oz;
    p.splitX = cx + ox;
    p.splitZ = cz + oz;
    p.hasSplit = true;

    const rdx = p.splitX - p.x;
    const rdz = p.splitZ - p.z;
    const d = Math.hypot(rdx, rdz);
    if (d > 1e-9) {
      this._splitDirX.set(sessionId, rdx / d);
      this._splitDirZ.set(sessionId, rdz / d);
    } else {
      this._splitDirX.set(sessionId, 1);
      this._splitDirZ.set(sessionId, 0);
    }
    this._splitInitSep.set(sessionId, d);
    this._splitPullStart.set(sessionId, Date.now() / 1000);
  }

  /** Recompute split mass from primary + time-eased separation along the fixed launch axis. */
  private applySplitPull(p: PlayerState) {
    if (!p.hasSplit) return;
    const id = p.id;
    const t0 = this._splitPullStart.get(id);
    if (t0 === undefined) return;
    const elapsed = Date.now() / 1000 - t0;
    const u = Math.min(1, elapsed / SPLIT_PULL_DURATION_SEC);
    const smooth = u * u * (3 - 2 * u);
    const initSep = this._splitInitSep.get(id) ?? 0;
    const dirX = this._splitDirX.get(id) ?? 1;
    const dirZ = this._splitDirZ.get(id) ?? 0;
    const sep = initSep * (1 - smooth);
    p.splitX = p.x + dirX * sep;
    p.splitZ = p.z + dirZ * sep;
    p.splitX = Math.max(-MAP_SIZE, Math.min(MAP_SIZE, p.splitX));
    p.splitZ = Math.max(-MAP_SIZE, Math.min(MAP_SIZE, p.splitZ));
  }

  private clearSplitPullData(sessionId: string) {
    this._splitPullStart.delete(sessionId);
    this._splitInitSep.delete(sessionId);
    this._splitDirX.delete(sessionId);
    this._splitDirZ.delete(sessionId);
  }

  private tryMergeSplitHalves(p: PlayerState) {
    if (!p.hasSplit) return;
    const dx = p.x - p.splitX;
    const dz = p.z - p.splitZ;
    const dist = Math.sqrt(dx * dx + dz * dz);
    const ra = this.getVisualScaleFromScore(p.score) * 0.4;
    const rb = this.getVisualScaleFromScore(p.splitScore) * 0.4;
    if (dist < (ra + rb) * MERGE_DIST_FACTOR) {
      p.score += p.splitScore;
      p.size += p.splitSize;
      p.hasSplit = false;
      p.splitScore = 0;
      p.splitSize = 0;
      this.clearSplitPullData(p.id);
    }
  }

  // Check if player is close enough to any blob pickups (spatially filtered).
  checkBlobPickups(p: PlayerState, blobGrid: Map<string, string[]>) {
    this.checkPickupsForMass(p, p.x, p.z, true, blobGrid);
    if (p.hasSplit) {
      this.checkPickupsForMass(p, p.splitX, p.splitZ, false, blobGrid);
    }
  }

  private checkPickupsForMass(
    p: PlayerState,
    px: number,
    pz: number,
    isPrimary: boolean,
    blobGrid: Map<string, string[]>,
  ) {
    const massScore = isPrimary ? p.score : p.splitScore;
    const t = Math.min(massScore / MAX_SCORE_FOR_SCALE, 1);
    const visualSize = 1 + t * 9;
    const pickupR = visualSize * 0.6;
    const pickupRSq = pickupR * pickupR;

    const minIx = Math.floor((px - pickupR) / BLOB_CELL_SIZE);
    const maxIx = Math.floor((px + pickupR) / BLOB_CELL_SIZE);
    const minIz = Math.floor((pz - pickupR) / BLOB_CELL_SIZE);
    const maxIz = Math.floor((pz + pickupR) / BLOB_CELL_SIZE);

    for (let ix = minIx; ix <= maxIx; ix++) {
      for (let iz = minIz; iz <= maxIz; iz++) {
        const ids = blobGrid.get(`${ix},${iz}`);
        if (!ids) continue;
        for (let i = 0; i < ids.length; i++) {
          const key = ids[i];
          const blob = this.state.blobs.get(key);
          if (!blob) continue;

          const dx = px - blob.x;
          const dz = pz - blob.z;
          if (dx * dx + dz * dz >= pickupRSq) continue;

          if (blob.isSpeedBoost) {
            this._speedBoostUntil.set(
              p.id,
              Date.now() / 1000 + SPEED_BOOST_DURATION_SEC,
            );
          } else if (blob.isSpeedSlow) {
            this._speedSlowUntil.set(
              p.id,
              Date.now() / 1000 + SPEED_BOOST_DURATION_SEC,
            );
          } else {
            const growthFactor = 1 / (1 + Math.log1p(massScore / 800));
            const add = blob.value * 0.014 * growthFactor;
            if (isPrimary) {
              p.size += add;
              p.score += blob.value;
            } else {
              p.splitSize += add;
              p.splitScore += blob.value;
            }
          }
          this.state.blobs.delete(key);
          this.spawnBlobs(1);
        }
      }
    }
  }

  getVisualScaleFromScore(score: number): number {
    const MIN_SCALE = 1;
    const MAX_SCALE = 10;
    const t = Math.min(score / MAX_SCORE_FOR_SCALE, 1);
    return MIN_SCALE + t * (MAX_SCALE - MIN_SCALE);
  }

  getVisualScale(p: PlayerState): number {
    return this.getVisualScaleFromScore(p.score);
  }

  // Check for collisions between players (each mass cell independently).
  checkPlayerCollisions() {
    const players = Array.from(this.state.players.values()) as PlayerState[];
    outer: for (let i = 0; i < players.length; i++) {
      for (let j = i + 1; j < players.length; j++) {
        const a = players[i];
        const b = players[j];
        if (!a.isAlive || !b.isAlive) continue;
        if (a.isInvincible || b.isInvincible) continue;

        const massesA = this.getMassCells(a);
        const massesB = this.getMassCells(b);
        for (const ma of massesA) {
          for (const mb of massesB) {
            const scaleA = this.getVisualScaleFromScore(ma.score);
            const scaleB = this.getVisualScaleFromScore(mb.score);
            const dist = Math.sqrt((ma.x - mb.x) ** 2 + (ma.z - mb.z) ** 2);
            if (dist >= (scaleA + scaleB) * 0.4) continue;
            if (ma.score > mb.score) {
              this.absorbMass(a, ma.primary, b, mb.primary);
              break outer;
            }
            if (mb.score > ma.score) {
              this.absorbMass(b, mb.primary, a, ma.primary);
              break outer;
            }
          }
        }
      }
    }
  }

  private getMassCells(p: PlayerState): {
    primary: boolean;
    x: number;
    z: number;
    score: number;
  }[] {
    if (!p.hasSplit) {
      return [{ primary: true, x: p.x, z: p.z, score: p.score }];
    }
    return [
      { primary: true, x: p.x, z: p.z, score: p.score },
      {
        primary: false,
        x: p.splitX,
        z: p.splitZ,
        score: p.splitScore,
      },
    ];
  }

  private absorbMass(
    winner: PlayerState,
    winPrimary: boolean,
    loser: PlayerState,
    losePrimary: boolean,
  ) {
    const loseScore = losePrimary ? loser.score : loser.splitScore;
    const loseSize = losePrimary ? loser.size : loser.splitSize;

    if (winPrimary) winner.score += loseScore;
    else winner.splitScore += loseScore;
    winner.size += loseSize * 0.5;

    if (!loser.hasSplit) {
      winner.kills += 1;
      this.killPlayerFully(loser, winner.name);
      return;
    }

    if (losePrimary) {
      loser.score = loser.splitScore;
      loser.size = loser.splitSize;
      loser.x = loser.splitX;
      loser.z = loser.splitZ;
      loser.hasSplit = false;
      loser.splitScore = 0;
      loser.splitSize = 0;
      this.clearSplitPullData(loser.id);
    } else {
      loser.hasSplit = false;
      loser.splitScore = 0;
      loser.splitSize = 0;
      this.clearSplitPullData(loser.id);
    }
  }

  private killPlayerFully(loser: PlayerState, killerName: string) {
    const finalScore = loser.score;
    loser.isAlive = false;
    loser.size = 1;
    loser.score = 0;
    loser.hasSplit = false;
    loser.splitScore = 0;
    loser.splitSize = 0;
    this.clearSplitPullData(loser.id);
    this._speedBoostUntil.delete(loser.id);
    this._speedSlowUntil.delete(loser.id);
    loser.speedBoostActive = false;
    loser.speedSlowActive = false;

    for (const c of this.clients) {
      if (c.sessionId === loser.id) {
        c.send("died", { killedBy: killerName, finalScore });
      }
    }
  }

  // Spawn a number of blob pickups at random positions
  spawnBlobs(count: number) {
    for (let i = 0; i < count; i++) {
      const blob = new BlobPickup();
      blob.id = Math.random().toString(36).substr(2, 9);
      // Full arena on X/Z (±MAP_SIZE, same as player clamp).
      blob.x = (Math.random() - 0.5) * 2 * MAP_SIZE;
      blob.z = (Math.random() - 0.5) * 2 * MAP_SIZE;
      blob.isSpecial = false;
      blob.isSpeedBoost = false;
      blob.isSpeedSlow = false;
      blob.color = randomBlobPresetColor();

      const roll = Math.random();
      if (roll < SPECIAL_BLOB_CHANCE) {
        // Special item: 3000-4000 points
        blob.value = Math.floor(Math.random() * 1001) + 3000;
        blob.isSpecial = true;
      } else if (roll < SPECIAL_BLOB_CHANCE + SPEED_BLOB_CHANCE) {
        blob.value = 0;
        blob.isSpeedBoost = true;
      } else if (
        roll <
        SPECIAL_BLOB_CHANCE + SPEED_BLOB_CHANCE + SLOW_BLOB_CHANCE
      ) {
        blob.value = 0;
        blob.isSpeedSlow = true;
      } else {
        blob.value =
          BLOB_VALUES[Math.floor(Math.random() * BLOB_VALUES.length)];
      }

      this.state.blobs.set(blob.id, blob);
    }
  }
}
