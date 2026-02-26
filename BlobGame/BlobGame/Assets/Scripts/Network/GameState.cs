using Colyseus.Schema;

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
}

public partial class BlobPickup : Schema
{
    [Type(0, "string")] public string id = default;
    [Type(1, "float32")] public float x = default;
    [Type(2, "float32")] public float z = default;
    [Type(3, "float32")] public float value = default;
}

public partial class GameState : Schema
{
    [Type(0, "map", typeof(MapSchema<PlayerState>))]
    public MapSchema<PlayerState> players = new MapSchema<PlayerState>();

    [Type(1, "map", typeof(MapSchema<BlobPickup>))]
    public MapSchema<BlobPickup> blobs = new MapSchema<BlobPickup>();
}