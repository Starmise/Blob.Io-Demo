import { defineServer, defineRoom } from "colyseus";
import { GameRoom } from "./rooms/MyRoom.js";

const server = defineServer({
  rooms: {
    game_room: defineRoom(GameRoom), // Register the game room with a unique name
  },
});

export default server;