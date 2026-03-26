import { Schema, MapSchema, ArraySchema, type } from "@colyseus/schema";

/** One secondary mass cell (primary stays on PlayerState x/z/score/size). */
export class SplitCell extends Schema {
  @type("float32") x: number = 0;
  @type("float32") z: number = 0;
  @type("int32") score: number = 0;
  @type("float32") size: number = 0;
}

// Define the state schema for the game room
export class PlayerState extends Schema {
  @type("string") id: string = "";
  @type("string") name: string = "Player";
  @type("string") color: string = "#FFF400"; // Default color for players
  @type("float32") x: number = 0;
  @type("float32") y: number = 0;
  @type("float32") z: number = 0;
  @type("float32") size: number = 1;
  @type("int32") kills: number = 0;
  @type("int32") score: number = 0;
  @type("boolean") isAlive: boolean = true;
  @type("boolean") isInvincible: boolean = false;
  @type("float32") invincibilityEndTime: number = 0;
  @type("string") skinId: string = "default";
  /** Synced for clients (speed boost pickup VFX). */
  @type("boolean") speedBoostActive: boolean = false;
  /** Synced for clients (slow debuff pickup VFX). */
  @type("boolean") speedSlowActive: boolean = false;
  /** Extra masses while split (Agar-style); primary stays at x,z / score / size. Max 4 extras. */
  @type([SplitCell]) splitCells = new ArraySchema<SplitCell>();
}

// Define the blob pickup schema
export class BlobPickup extends Schema {
  @type("string") id: string = "";
  @type("float32") x: number = 0;
  @type("float32") z: number = 0;
  @type("float32") value: number = 1;
  @type("boolean") isSpecial: boolean = false;
  /** CSS hex color for client rendering (independent of value). */
  @type("string") color: string = "#ffffff";
  @type("boolean") isSpeedBoost: boolean = false;
  /** Same sprite as boost; server + client use tint / VFX to differ. */
  @type("boolean") isSpeedSlow: boolean = false;
}

// Define the overall game state schema
export class GameState extends Schema {
  @type({ map: PlayerState }) players = new MapSchema<PlayerState>();
  @type({ map: BlobPickup }) blobs = new MapSchema<BlobPickup>();
}
