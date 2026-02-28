import { Room, Client } from "colyseus";
import { GameState, PlayerState, BlobPickup } from "./schema/MyRoomState.js";

const MAP_SIZE = 50;
const BASE_SPEED = 0.5;
const BLOB_COUNT = 800;
const INVINCIBLE_SEC = 10;
const BLOB_VALUES = [1, 2, 5, 10];
const GOLDEN_BLOB_CHANCE = 0.05;
// Controls how quickly players visually "grow" from score.
// This MUST match the client-side scale curve (Unity `PlayerController`).
const MAX_SCORE_FOR_SCALE = 50000;

// cd BlobGame/server/blob-server // yarn start

export class GameRoom extends Room {
  declare state: GameState;

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
      (client, data: { color: string; killCost: number }) => {
        const p = this.state.players.get(client.sessionId);
        if (!p) return;
        if (p.kills < data.killCost) return;
        p.kills -= data.killCost;
        p.color = data.color;
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
    });
  }

  onJoin(client: Client, options: any) {
    const p = new PlayerState();
    p.id = client.sessionId;
    p.name = (options.name || "Player").substring(0, 16);
    p.color = options.color || "#4488FF";
    p.x = (Math.random() - 0.5) * MAP_SIZE; // Spawn in random position
    p.z = (Math.random() - 0.5) * MAP_SIZE; // Y is fixed since it's a "top-down" game
    p.y = 0;
    p.size = 1;
    p.isAlive = true;
    p.isInvincible = true;
    p.invincibilityEndTime = Date.now() / 1000 + INVINCIBLE_SEC; // Invincibility on spawn for 10 seconds
    this.state.players.set(client.sessionId, p);
    console.log(`[JOIN] ${p.name}`);
  }

  // Handle player leaving the room
  onLeave(client: Client) {
    this.state.players.delete(client.sessionId);
    console.log(`[LEAVE] ${client.sessionId}`);
  }

  update(dt: number) {
    const now = Date.now() / 1000;
    this.state.players.forEach((p: PlayerState) => {
      // Update invincibility and check pickups
      if (!p.isAlive) return;
      if (p.isInvincible && now >= p.invincibilityEndTime) {
        p.isInvincible = false;
      }
      p.x = Math.max(-MAP_SIZE, Math.min(MAP_SIZE, p.x)); // Keep player within bounds
      p.z = Math.max(-MAP_SIZE, Math.min(MAP_SIZE, p.z)); // Keep player within bounds
      this.checkBlobPickups(p);
    });
    this.checkPlayerCollisions();
  }

  // Handle player movement input
  handleMove(sessionId: string, inputX: number, inputZ: number) {
    const p = this.state.players.get(sessionId);
    if (!p || !p.isAlive) return;
    const len = Math.sqrt(inputX * inputX + inputZ * inputZ);
    if (len === 0) return;

    // ***Límite mínimo de velocidad para que nunca se congele***
    const speed = Math.max(BASE_SPEED / (1 + p.size * 0.15), 0.05);
    const dt = 1 / 20;
    p.x += (inputX / len) * speed * dt;
    p.z += (inputZ / len) * speed * dt;
    p.x = Math.max(-MAP_SIZE, Math.min(MAP_SIZE, p.x));
    p.z = Math.max(-MAP_SIZE, Math.min(MAP_SIZE, p.z));
  }

  // Check if player is close enough to any blob pickups
  checkBlobPickups(p: PlayerState) {
    this.state.blobs.forEach((blob: BlobPickup, key: string) => {
      const dx = p.x - blob.x;
      const dz = p.z - blob.z;
      const dist = Math.sqrt(dx * dx + dz * dz);

      // Same calculations as in Unity for visual size.
      const t = Math.min(p.score / MAX_SCORE_FOR_SCALE, 1);
      const visualSize = 1 + t * 9; // MIN=1, MAX=10

      if (dist < visualSize * 0.6) {
        p.size += blob.value * 0.01;
        p.score += blob.value;
        this.state.blobs.delete(key);
        this.spawnBlobs(1);
      }
    });
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
        blob.x  = (Math.random() - 0.5) * MAP_SIZE * 1.8;
        blob.z  = (Math.random() - 0.5) * MAP_SIZE * 1.8;

        // 5% chance of golden blob
        if (Math.random() < GOLDEN_BLOB_CHANCE) {
            blob.value  = Math.floor(Math.random() * 2001) + 1000; // 1000-3000
            blob.isGolden = true;
        } else {
            blob.value    = BLOB_VALUES[Math.floor(Math.random() * BLOB_VALUES.length)];
            blob.isGolden = false;
        }

        this.state.blobs.set(blob.id, blob);
    }
}
}
