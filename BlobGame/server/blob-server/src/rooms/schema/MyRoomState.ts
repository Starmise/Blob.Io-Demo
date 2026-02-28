import { Schema, MapSchema, type } from "@colyseus/schema";

// Define the state schema for the game room
export class PlayerState extends Schema {
  @type("string")  id: string = "";
  @type("string")  name: string = "Player";
  @type("string")  color: string = "#ffdd44"; // Default color for players
  @type("float32") x: number = 0;
  @type("float32") y: number = 0;
  @type("float32") z: number = 0;
  @type("float32") size: number = 1;
  @type("int32")   kills: number = 0;
  @type("int32")   score: number = 0;
  @type("boolean") isAlive: boolean = true;
  @type("boolean") isInvincible: boolean = false;
  @type("float32") invincibilityEndTime: number = 0;
}

// Define the blob pickup schema
export class BlobPickup extends Schema {
  @type("string")  id: string = "";
  @type("float32") x: number = 0;
  @type("float32") z: number = 0;
  @type("float32") value: number = 1;
  @type("boolean") isGolden: boolean = false;
}

// Define the overall game state schema
export class GameState extends Schema {
  @type({ map: PlayerState }) players = new MapSchema<PlayerState>();
  @type({ map: BlobPickup })  blobs   = new MapSchema<BlobPickup>();
}