using Colyseus.Schema;

/// <summary>
/// Player state schema that defines the properties of a player in the game. This is used by
/// Colyseus to synchronize the player state between the server and clients. Each property has a type and an index, 
/// which is used for efficient serialization.The GameState class contains a map of player states and blob pickups, 
/// which represent the current state of the game. The GameManager listens to these changes to update the scene accordingly.
/// </summary>
public partial class PlayerState : Schema
{
    [Type(0, "string")] public string id = default;
    [Type(1, "string")] public string name = default;
    [Type(2, "string")] public string color = default;
    [Type(3, "float32")] public float x = default;
    [Type(4, "float32")] public float y = default;
    [Type(5, "float32")] public float z = default;
    [Type(6, "float32")] public float size = default;
    [Type(7, "int32")] public int kills = default;
    [Type(8, "int32")] public int score = default;
    [Type(9, "boolean")] public bool isAlive = default;
    [Type(10, "boolean")] public bool isInvincible = default;
    [Type(11, "float32")] public float invincibilityEndTime = default;
    [Type(12, "string")] public string skinId = default;
    [Type(13, "boolean")] public bool speedBoostActive = default;
    [Type(14, "boolean")] public bool speedSlowActive = default;
    [Type(15, "boolean")] public bool hasSplit = default;
    [Type(16, "float32")] public float splitX = default;
    [Type(17, "float32")] public float splitZ = default;
    [Type(18, "int32")] public int splitScore = default;
    [Type(19, "float32")] public float splitSize = default;
}

/// <summary>
/// Blob pickup schema that defines the properties of a blob pickup item in the game. This is used by
/// Colyseus to synchronize the blob pickup state between the server and clients.
/// </summary>
public partial class BlobPickup : Schema
{
    [Type(0, "string")] public string id = default;
    [Type(1, "float32")] public float x = default;
    [Type(2, "float32")] public float z = default;
    [Type(3, "float32")] public float value = default;
    [Type(4, "boolean")] public bool isSpecial = default;
    [Type(5, "string")] public string color = default;
    [Type(6, "boolean")] public bool isSpeedBoost = default;
    [Type(7, "boolean")] public bool isSpeedSlow = default;
}

/// <summary>
/// Game state schema that contains the overall state of the game, including the players and blob pickups.
/// This is the main state object that the server updates and the clients listen to for changes.
/// </summary>
public partial class GameState : Schema
{
    [Type(0, "map", typeof(MapSchema<PlayerState>))]
    public MapSchema<PlayerState> players = new MapSchema<PlayerState>();

    [Type(1, "map", typeof(MapSchema<BlobPickup>))]
    public MapSchema<BlobPickup> blobs = new MapSchema<BlobPickup>();
}