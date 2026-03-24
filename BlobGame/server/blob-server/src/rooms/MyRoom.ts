import { Room, Client } from "colyseus";
import { GameState, PlayerState, BlobPickup } from "./schema/MyRoomState.js";

// Half-width of the arena in world units (players stay within [-MAP_SIZE, MAP_SIZE] on X/Z).
// Must match the Unity Game scene: Plane scale (2*MAP_SIZE+2)/10 on X/Z, walls at ±(MAP_SIZE+1).
const MAP_SIZE = 75;
const BASE_SPEED = 0.9;
/** Target blob count (high — use spatial grid in update for O(n) pickup checks). */
const BLOB_COUNT = 4000;
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

// Movement: gentler slowdown vs score (higher ref = same score feels "lighter").
const SPEED_SCORE_REF = 8000;
const SPEED_LOG_WEIGHT = 0.14;
// Biggest players still move at least this fraction of BASE_SPEED (was a harsh flat 0.3).
const MIN_SPEED_RATIO = 0.64;

/** Pickup tint options — edit to taste; must stay in sync with client expectations (CSS hex). */
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

  onCreate(options: any) {
    this.state = new GameState();
    this.spawnBlobs(BLOB_COUNT);

    this.setSimulationInterval((dt) => this.update(dt), 1000 / 20);

    // Movement
    this.onMessage("move", (client, data: { x: number; z: number }) => {
      this.handleMove(client.sessionId, data.x, data.z);
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

    // Slowdown vs score: same BASE_SPEED at low score, milder drop-off when huge.
    const speedT = Math.log1p(p.score / SPEED_SCORE_REF) * SPEED_LOG_WEIGHT;
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
    p.x += (inputX / len) * speed * dt;
    p.z += (inputZ / len) * speed * dt;
    p.x = Math.max(-MAP_SIZE, Math.min(MAP_SIZE, p.x));
    p.z = Math.max(-MAP_SIZE, Math.min(MAP_SIZE, p.z));
  }

  // Check if player is close enough to any blob pickups (spatially filtered).
  checkBlobPickups(p: PlayerState, blobGrid: Map<string, string[]>) {
    const t = Math.min(p.score / MAX_SCORE_FOR_SCALE, 1);
    const visualSize = 1 + t * 9;
    const pickupR = visualSize * 0.6;
    const pickupRSq = pickupR * pickupR;

    const minIx = Math.floor((p.x - pickupR) / BLOB_CELL_SIZE);
    const maxIx = Math.floor((p.x + pickupR) / BLOB_CELL_SIZE);
    const minIz = Math.floor((p.z - pickupR) / BLOB_CELL_SIZE);
    const maxIz = Math.floor((p.z + pickupR) / BLOB_CELL_SIZE);

    for (let ix = minIx; ix <= maxIx; ix++) {
      for (let iz = minIz; iz <= maxIz; iz++) {
        const ids = blobGrid.get(`${ix},${iz}`);
        if (!ids) continue;
        for (let i = 0; i < ids.length; i++) {
          const key = ids[i];
          const blob = this.state.blobs.get(key);
          if (!blob) continue;

          const dx = p.x - blob.x;
          const dz = p.z - blob.z;
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
            // Slower stat growth at high score (visual scale is score-driven; this stays in sync loosely).
            const growthFactor = 1 / (1 + Math.log1p(p.score / 800));
            p.size += blob.value * 0.014 * growthFactor;
            p.score += blob.value;
          }
          this.state.blobs.delete(key);
          this.spawnBlobs(1);
        }
      }
    }
  }

  getVisualScale(p: PlayerState): number {
    const MIN_SCALE = 1;
    const MAX_SCALE = 10;
    const t = Math.min(p.score / MAX_SCORE_FOR_SCALE, 1);
    return MIN_SCALE + t * (MAX_SCALE - MIN_SCALE);
  }

  // Check for collisions between players
  checkPlayerCollisions() {
    const players = Array.from(this.state.players.values()) as PlayerState[];
    for (let i = 0; i < players.length; i++) {
      for (let j = i + 1; j < players.length; j++) {
        const a = players[i];
        const b = players[j];
        if (!a.isAlive || !b.isAlive) continue;
        if (a.isInvincible || b.isInvincible) continue;

        const scaleA = this.getVisualScale(a);
        const scaleB = this.getVisualScale(b);
        const dist = Math.sqrt((a.x - b.x) ** 2 + (a.z - b.z) ** 2);

        // Colisión basada en escala visual; el ganador depende de los puntos.
        if (dist < (scaleA + scaleB) * 0.4) {
          if (a.score > b.score) this.consumePlayer(a, b);
          else if (b.score > a.score) this.consumePlayer(b, a);
        }
      }
    }
  }

  // Handle one player consuming another
  consumePlayer(winner: PlayerState, loser: PlayerState) {
    winner.size += loser.size * 0.5;
    winner.score += loser.score;
    winner.kills += 1;
    const finalScore = loser.score;
    loser.isAlive = false;
    loser.size = 1;
    loser.score = 0;
    this._speedBoostUntil.delete(loser.id);
    this._speedSlowUntil.delete(loser.id);
    loser.speedBoostActive = false;
    loser.speedSlowActive = false;

    // Notify the loser — client will handle respawn timing
    for (const c of this.clients) {
      if (c.sessionId === loser.id) {
        c.send("died", { killedBy: winner.name, finalScore });
      }
    }

    // No automatic respawn — client requests it via "requestRespawn"
  }

  // Spawn a number of blob pickups at random positions
  spawnBlobs(count: number) {
    for (let i = 0; i < count; i++) {
      const blob = new BlobPickup();
      blob.id = Math.random().toString(36).substr(2, 9);
      blob.x = (Math.random() - 0.5) * MAP_SIZE * 1.8;
      blob.z = (Math.random() - 0.5) * MAP_SIZE * 1.8;
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
