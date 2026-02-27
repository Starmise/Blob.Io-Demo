import { Room, Client } from "colyseus";
import { GameState, PlayerState, BlobPickup } from "./schema/MyRoomState.js";

const MAP_SIZE = 50;
const BASE_SPEED = 0.5;
const BLOB_COUNT = 400;
const INVINCIBLE_SEC = 10;
const BLOB_VALUES = [1, 2, 5, 10];

export class GameRoom extends Room {
  declare state: GameState;

  onCreate(options: any) {
    this.state = new GameState();
    this.spawnBlobs(BLOB_COUNT);

    this.setSimulationInterval((dt) => this.update(dt), 1000 / 20); // 20 ticks per second

    // Handle player movement input
    this.onMessage("move", (client, data: { x: number; z: number }) => {
      this.handleMove(client.sessionId, data.x, data.z); //
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

    // Límite mínimo de velocidad para que nunca se congele
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

      // Mismo cálculo de escala visual que Unity
      const t = Math.min(p.score / 500000, 1);
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
    const MAX_SCORE = 500000;
    const t = Math.min(p.score / MAX_SCORE, 1);
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

        // Colisión basada en escala visual
        if (dist < (scaleA + scaleB) * 0.4) {
          if (scaleA > scaleB * 1.1) this.consumePlayer(a, b);
          else if (scaleB > scaleA * 1.1) this.consumePlayer(b, a);
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

    // Notify the loser about their death and who killed them
    this.clients.forEach((c) => {
      if (c.sessionId === loser.id) {
        c.send("died", { killedBy: winner.name, finalScore });
      }
    });

    // Respawn after 3 seconds with invincibility
    setTimeout(() => {
      loser.x = (Math.random() - 0.5) * MAP_SIZE;
      loser.z = (Math.random() - 0.5) * MAP_SIZE;
      loser.isAlive = true;
      loser.isInvincible = true;
      loser.invincibilityEndTime = Date.now() / 1000 + INVINCIBLE_SEC;
    }, 3000);
  }

  // Spawn a number of blob pickups at random positions
  spawnBlobs(count: number) {
    for (let i = 0; i < count; i++) {
      const blob = new BlobPickup();
      blob.id = Math.random().toString(36).substr(2, 9);
      // Antes era MAP_SIZE * 2, ahora dentro del mapa
      blob.x = (Math.random() - 0.5) * MAP_SIZE * 1.8;
      blob.z = (Math.random() - 0.5) * MAP_SIZE * 1.8;
      blob.value = BLOB_VALUES[Math.floor(Math.random() * BLOB_VALUES.length)];
      this.state.blobs.set(blob.id, blob);
    }
  }
}
