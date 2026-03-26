import { Room, Client } from "colyseus";
import {
  GameState,
  PlayerState,
  BlobPickup,
  SplitCell,
} from "./schema/MyRoomState.js";

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
const SPECIAL_BLOB_CHANCE = 0.002;
const SPEED_BLOB_CHANCE = 0.01;
const SLOW_BLOB_CHANCE = 0.005;
const SPEED_BOOST_MULTIPLIER = 1.35;
/** Mirrors boost strength: move speed × this while slow debuff is active. */
const SPEED_SLOW_MULTIPLIER = 1 / SPEED_BOOST_MULTIPLIER;
const SPEED_BOOST_DURATION_SEC = 4;
// Controls how quickly players visually "grow" from score.
// This MUST match the client-side scale curve (Unity `PlayerController`).
const MAX_SCORE_FOR_SCALE = 200000;

// Movement: io-style — barely slower when huge.
const SPEED_SCORE_REF = MAX_SCORE_FOR_SCALE;
const SPEED_LOG_WEIGHT = 0.04;
// Safety floor for absurd scores; normal play stays above this via the soft curve.
const MIN_SPEED_RATIO = 0.95;

/** Center-to-center distance between split halves (smaller = easier to merge back). */
const SPLIT_SEPARATION = 1.65;
/** Merge when distance < (rA + rB) * this. */
const MERGE_DIST_FACTOR = 0.42;
/** Over this many seconds each extra mass eases toward the primary (smoothstep). */
const SPLIT_PULL_DURATION_SEC = 14;

/** Max extra masses (primary + 4 = 5 cells total). */
const MAX_SPLIT_EXTRAS = 4;

/** Pickup tint options — edit to taste; must stay in sync with client expectations. */
const BLOB_PICKUP_COLORS = [
  "#ff6b6b",
  "#4ecdc4",
  "#ffe66d",
  "#a29bfe",
  "#fd79a8",
  "#74b9ff",
] as const;

type SplitPullMeta = {
  t0: number;
  initSep: number;
  dirX: number;
  dirZ: number;
};

type MassCell = {
  primary: boolean;
  index: number;
  x: number;
  z: number;
  score: number;
};

function randomBlobPresetColor(): string {
  return BLOB_PICKUP_COLORS[
    Math.floor(Math.random() * BLOB_PICKUP_COLORS.length)
  ];
}

function totalMassScore(p: PlayerState): number {
  let t = p.score;
  for (let i = 0; i < p.splitCells.length; i++) t += p.splitCells[i].score;
  return t;
}

// cd BlobGame/server/blob-server // yarn start

export class GameRoom extends Room {
  declare state: GameState;
  private readonly _speedBoostUntil = new Map<string, number>();
  private readonly _speedSlowUntil = new Map<string, number>();
  /** Per extra cell: pull toward primary along fixed axis from split time. */
  private readonly _splitPullMeta = new Map<string, SplitPullMeta[]>();

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
      p.splitCells.clear();
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
      if (p.splitCells.length > 0) {
        this.applySplitPull(p);
      }
      this.tryMergeOwnCells(p);
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

    const totalScore = totalMassScore(p);
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
    if (p.splitCells.length > 0) {
      this.applySplitPull(p);
    }
  }

  private findLargestCellForSplit(p: PlayerState): {
    kind: "primary" | "extra";
    index: number;
    score: number;
    size: number;
    cx: number;
    cz: number;
  } | null {
    let best: {
      kind: "primary" | "extra";
      index: number;
      score: number;
      size: number;
      cx: number;
      cz: number;
    } = {
      kind: "primary",
      index: -1,
      score: p.score,
      size: p.size,
      cx: p.x,
      cz: p.z,
    };
    for (let i = 0; i < p.splitCells.length; i++) {
      const c = p.splitCells[i];
      if (c.score > best.score) {
        best = {
          kind: "extra",
          index: i,
          score: c.score,
          size: c.size,
          cx: c.x,
          cz: c.z,
        };
      }
    }
    if (best.score < 2) return null;
    return best;
  }

  private makePullMeta(
    primX: number,
    primZ: number,
    cellX: number,
    cellZ: number,
  ): SplitPullMeta {
    const rdx = cellX - primX;
    const rdz = cellZ - primZ;
    const d = Math.hypot(rdx, rdz) || 1e-9;
    return {
      t0: Date.now() / 1000,
      initSep: d,
      dirX: rdx / d,
      dirZ: rdz / d,
    };
  }

  private handleSplit(sessionId: string, dirX: number, dirZ: number) {
    const p = this.state.players.get(sessionId);
    if (!p || !p.isAlive) return;
    if (p.splitCells.length >= MAX_SPLIT_EXTRAS) return;

    const pick = this.findLargestCellForSplit(p);
    if (!pick) return;

    const halfScore = Math.floor(pick.score / 2);
    if (halfScore < 1) return;

    const sz = pick.size;
    const halfSize = sz * 0.5;

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

    const cx = pick.cx;
    const cz = pick.cz;
    const backX = cx - ox;
    const backZ = cz - oz;
    const fwdX = cx + ox;
    const fwdZ = cz + oz;

    let metas = this._splitPullMeta.get(sessionId);
    if (!metas) {
      metas = [];
      this._splitPullMeta.set(sessionId, metas);
    }

    if (pick.kind === "primary") {
      p.x = backX;
      p.z = backZ;
      p.score = halfScore;
      p.size = halfSize;

      const cell = new SplitCell();
      cell.x = fwdX;
      cell.z = fwdZ;
      cell.score = halfScore;
      cell.size = halfSize;
      p.splitCells.push(cell);

      metas.push(this.makePullMeta(p.x, p.z, fwdX, fwdZ));
    } else {
      const i = pick.index;
      const cell = p.splitCells[i];
      cell.x = backX;
      cell.z = backZ;
      cell.score = halfScore;
      cell.size = halfSize;

      const newCell = new SplitCell();
      newCell.x = fwdX;
      newCell.z = fwdZ;
      newCell.score = halfScore;
      newCell.size = halfSize;
      p.splitCells.push(newCell);

      metas[i] = this.makePullMeta(p.x, p.z, backX, backZ);
      metas.push(this.makePullMeta(p.x, p.z, fwdX, fwdZ));
    }
  }

  /** Recompute extra masses from primary + time-eased separation along fixed axes. */
  private applySplitPull(p: PlayerState) {
    const metas = this._splitPullMeta.get(p.id);
    if (!metas || metas.length !== p.splitCells.length) return;
    const now = Date.now() / 1000;
    for (let i = 0; i < p.splitCells.length; i++) {
      const cell = p.splitCells[i];
      const meta = metas[i];
      const elapsed = now - meta.t0;
      const u = Math.min(1, elapsed / SPLIT_PULL_DURATION_SEC);
      const smooth = u * u * (3 - 2 * u);
      const sep = meta.initSep * (1 - smooth);
      cell.x = p.x + meta.dirX * sep;
      cell.z = p.z + meta.dirZ * sep;
      cell.x = Math.max(-MAP_SIZE, Math.min(MAP_SIZE, cell.x));
      cell.z = Math.max(-MAP_SIZE, Math.min(MAP_SIZE, cell.z));
    }
  }

  private clearSplitPullData(sessionId: string) {
    this._splitPullMeta.delete(sessionId);
  }

  private tryMergeOwnCells(p: PlayerState) {
    const metas = this._splitPullMeta.get(p.id);
    if (!metas || metas.length !== p.splitCells.length) return;

    type CellDesc = {
      isPrimary: boolean;
      index: number;
      x: number;
      z: number;
      score: number;
      size: number;
    };

    const cells: CellDesc[] = [
      {
        isPrimary: true,
        index: -1,
        x: p.x,
        z: p.z,
        score: p.score,
        size: p.size,
      },
    ];
    for (let i = 0; i < p.splitCells.length; i++) {
      const c = p.splitCells[i];
      cells.push({
        isPrimary: false,
        index: i,
        x: c.x,
        z: c.z,
        score: c.score,
        size: c.size,
      });
    }

    for (let i = 0; i < cells.length; i++) {
      for (let j = i + 1; j < cells.length; j++) {
        const a = cells[i];
        const b = cells[j];
        const dx = a.x - b.x;
        const dz = a.z - b.z;
        const dist = Math.sqrt(dx * dx + dz * dz);
        const ra = this.getVisualScaleFromScore(a.score) * 0.4;
        const rb = this.getVisualScaleFromScore(b.score) * 0.4;
        if (dist >= (ra + rb) * MERGE_DIST_FACTOR) continue;

        const aBigger = a.score >= b.score;
        const L = aBigger ? a : b;
        const S = aBigger ? b : a;
        const mergedScore = L.score + S.score;
        const mergedSize = L.size + S.size;

        if (L.isPrimary && !S.isPrimary) {
          p.score = mergedScore;
          p.size = mergedSize;
          p.splitCells.splice(S.index, 1);
          metas.splice(S.index, 1);
          return;
        }

        if (!L.isPrimary && S.isPrimary) {
          const li = L.index;
          const c = p.splitCells[li];
          p.x = c.x;
          p.z = c.z;
          p.score = mergedScore;
          p.size = mergedSize;
          p.splitCells.splice(li, 1);
          metas.splice(li, 1);
          return;
        }

        if (!L.isPrimary && !S.isPrimary) {
          const keepIdx = L.index;
          const dropIdx = S.index;
          if (dropIdx < keepIdx) {
            p.splitCells.splice(dropIdx, 1);
            metas.splice(dropIdx, 1);
            const newKeepIdx = keepIdx - 1;
            const cell = p.splitCells[newKeepIdx];
            cell.x = L.x;
            cell.z = L.z;
            cell.score = mergedScore;
            cell.size = mergedSize;
          } else {
            const cell = p.splitCells[keepIdx];
            cell.x = L.x;
            cell.z = L.z;
            cell.score = mergedScore;
            cell.size = mergedSize;
            p.splitCells.splice(dropIdx, 1);
            metas.splice(dropIdx, 1);
          }
          return;
        }
      }
    }
  }

  // Check if player is close enough to any blob pickups (spatially filtered).
  checkBlobPickups(p: PlayerState, blobGrid: Map<string, string[]>) {
    this.checkPickupsForMass(p, p.x, p.z, -1, blobGrid);
    for (let i = 0; i < p.splitCells.length; i++) {
      const c = p.splitCells[i];
      this.checkPickupsForMass(p, c.x, c.z, i, blobGrid);
    }
  }

  private checkPickupsForMass(
    p: PlayerState,
    px: number,
    pz: number,
    cellIndex: number,
    blobGrid: Map<string, string[]>,
  ) {
    const massScore =
      cellIndex < 0 ? p.score : p.splitCells[cellIndex].score;
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
            if (cellIndex < 0) {
              p.size += add;
              p.score += blob.value;
            } else {
              const c = p.splitCells[cellIndex];
              c.size += add;
              c.score += blob.value;
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
              this.absorbMass(a, ma, b, mb);
              break outer;
            }
            if (mb.score > ma.score) {
              this.absorbMass(b, mb, a, ma);
              break outer;
            }
          }
        }
      }
    }
  }

  private getMassCells(p: PlayerState): MassCell[] {
    const cells: MassCell[] = [
      { primary: true, index: -1, x: p.x, z: p.z, score: p.score },
    ];
    for (let i = 0; i < p.splitCells.length; i++) {
      const c = p.splitCells[i];
      cells.push({
        primary: false,
        index: i,
        x: c.x,
        z: c.z,
        score: c.score,
      });
    }
    return cells;
  }

  private getCellScoreSize(
    p: PlayerState,
    cell: MassCell,
  ): { score: number; size: number } {
    if (cell.primary) return { score: p.score, size: p.size };
    const c = p.splitCells[cell.index];
    return { score: c.score, size: c.size };
  }

  private absorbMass(
    winner: PlayerState,
    winCell: MassCell,
    loser: PlayerState,
    loseCell: MassCell,
  ) {
    const lose = this.getCellScoreSize(loser, loseCell);
    const loseScore = lose.score;
    const loseSize = lose.size;

    if (winCell.primary) winner.score += loseScore;
    else winner.splitCells[winCell.index].score += loseScore;
    winner.size += loseSize * 0.5;

    const hadOnlyPrimary = loser.splitCells.length === 0;

    if (loseCell.primary) {
      if (hadOnlyPrimary) {
        winner.kills += 1;
        this.killPlayerFully(loser, winner.name);
        return;
      }
      this.promoteBestExtraToPrimary(loser);
      return;
    }

    loser.splitCells.splice(loseCell.index, 1);
    const lm = this._splitPullMeta.get(loser.id);
    if (lm) lm.splice(loseCell.index, 1);
  }

  private promoteBestExtraToPrimary(p: PlayerState) {
    if (p.splitCells.length === 0) return;
    let bestI = 0;
    let best = p.splitCells[0].score;
    for (let i = 1; i < p.splitCells.length; i++) {
      if (p.splitCells[i].score > best) {
        best = p.splitCells[i].score;
        bestI = i;
      }
    }
    const c = p.splitCells[bestI];
    p.x = c.x;
    p.z = c.z;
    p.score = c.score;
    p.size = c.size;
    p.splitCells.splice(bestI, 1);
    const metas = this._splitPullMeta.get(p.id);
    if (metas) metas.splice(bestI, 1);
  }

  private killPlayerFully(loser: PlayerState, killerName: string) {
    const finalScore = totalMassScore(loser);
    loser.isAlive = false;
    loser.size = 1;
    loser.score = 0;
    loser.splitCells.clear();
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
